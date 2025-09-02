using Orcei.Models.Usuarios;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Linq;
using System.Net.Http;

namespace Orcei.ServicosConexao.Services
{
    public class ComparadorService
    {
        private readonly IHttpClientFactory _http;
        private readonly IConfiguration _cfg;

        private static readonly string[] AllowedStores = new[]
        {
            "Amazon",
            "Terabyte",
            "KaBuM!",
            "Pichau",
            "Mercado Livre",
            "Magazine Luiza"
        };

        private static readonly (string Canonical, string[] Aliases)[] StoreAliases = new[]
        {
            ("Amazon",         new[] { "amazon", "amazon.com", "amazon.com.br" }),
            ("Terabyte",       new[] { "terabyteshop", "terabyte", "terabyteshop.com.br" }),
            ("KaBuM!",         new[] { "kabum", "ka bum", "kabum.com.br" }),
            ("Pichau",         new[] { "pichau", "pichau.com.br" }),
            ("Mercado Livre",  new[] { "mercadolivre", "mercado livre", "mercadolivre.com.br" }),
            ("Magazine Luiza", new[] { "magazineluiza", "magazine luiza", "magalu", "magalu.com", "magazineluiza.com.br" }),
        };

        public ComparadorService(IHttpClientFactory http, IConfiguration cfg)
        {
            _http = http; _cfg = cfg;
        }

        public async Task<CompararPrecosResponse> CompareAsync(string categoria, string modelo, CancellationToken ct = default)
        {
            var termo = BuildQuery(categoria, modelo);

            var tSerpShopping = FromSerpApiShopping(termo, ct);
            var tMercadoLivre = FromMercadoLivreUsed(termo, ct); 
            var tOlx = FromOlxViaSerpApi(termo, ct); 
            var tEbay = FromEbayOptional(termo, ct); 

            await Task.WhenAll(tSerpShopping, tMercadoLivre, tOlx, tEbay);

            var novosPorLoja = (await tSerpShopping);
            var newSites = novosPorLoja
                .Select(kv => new SiteAverage(kv.Key, AvgTrimmed(kv.Value), kv.Value.Count))
                .OrderBy(x => x.Valor)
                .ToList();

            decimal? newAvg = newSites.Count > 0 ? AvgTrimmed(newSites.Select(x => x.Valor).ToList()) : null;

            var usados = new List<(string site, decimal price)>();
            usados.AddRange((await tMercadoLivre).Select(p => ("Mercado Livre", p)));
            usados.AddRange((await tOlx).Select(p => ("OLX", p)));
            usados.AddRange((await tEbay).Select(p => ("eBay", p)));

            var usedSites = usados
                .GroupBy(x => x.site)
                .Select(g => new SiteAverage(
                    Site: g.Key,
                    Valor: AvgTrimmed(g.Select(v => v.price).ToList()),
                    Qtd: g.Count()
                ))
                .OrderBy(x => x.Valor)
                .ToList();

            decimal? usedAvg = usedSites.Count > 0 ? AvgTrimmed(usedSites.Select(x => x.Valor).ToList()) : null;

            decimal? min = null, max = null;
            if (usedAvg is not null) { min = Round0(usedAvg.Value * 0.85m); max = Round0(usedAvg.Value * 1.20m); }
            else if (newAvg is not null) { min = Round0(newAvg.Value * 0.60m); max = Round0(newAvg.Value * 0.80m); }

            var newSources = newSites.Select(s => $"{s.Site} - R$ {s.Valor:N0} ({s.Qtd})").ToList();
            var usedSources = usedSites.Select(s => $"{s.Site} - R$ {s.Valor:N0} ({s.Qtd})").ToList();

            return new CompararPrecosResponse
            {
                NewPriceAvg = newAvg is null ? null : Round0(newAvg.Value),
                UsedPriceAvg = usedAvg is null ? null : Round0(usedAvg.Value),
                PriceMin = min,
                PriceMax = max,
                NewSites = newSites,
                UsedSites = usedSites,
                NewPriceSources = newSources,
                UsedPriceSources = usedSources
            };
        }

        private async Task<Dictionary<string, List<decimal>>> FromSerpApiShopping(string q, CancellationToken ct)
        {
            var key = _cfg["SERPAPI_KEY"];
            var outMap = new Dictionary<string, List<decimal>>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(key)) return outMap;

            var url = $"https://serpapi.com/search?engine=google_shopping&q={Uri.EscapeDataString(q)}&gl=br&hl=pt-BR&num=50&api_key={Uri.EscapeDataString(key)}";
            var cli = _http.CreateClient();

            try
            {
                using var resp = await cli.GetAsync(url, ct);
                var json = await resp.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("shopping_results", out var arr) || arr.ValueKind != JsonValueKind.Array)
                    return outMap;

                foreach (var it in arr.EnumerateArray())
                {
                    var source = it.TryGetProperty("source", out var s) ? (s.GetString() ?? "").Trim() : "";
                    var link = it.TryGetProperty("link", out var l) ? (l.GetString() ?? "").Trim() : "";

                    var store = NormalizeStore(source, link);
                    if (store is null) continue;
                    decimal price = 0m;
                    if (it.TryGetProperty("extracted_price", out var ep) && ep.ValueKind == JsonValueKind.Number)
                        price = ep.GetDecimal();
                    else if (it.TryGetProperty("price", out var ps))
                    {
                        var raw = ps.GetString() ?? "";
                        var m = Regex.Match(raw, @"([\d\.\,]+)");
                        if (m.Success)
                        {
                            var num = m.Groups[1].Value.Replace(".", "").Replace(",", ".");
                            if (decimal.TryParse(num, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var d))
                                price = d;
                        }
                    }
                    if (price <= 0) continue;

                    if (!outMap.TryGetValue(store, out var list)) outMap[store] = list = new();
                    list.Add(price);
                }

                foreach (var k in outMap.Keys.ToList())
                    if (!AllowedStores.Contains(k)) outMap.Remove(k);
            }
            catch { }

            return outMap;
        }

        private static string? NormalizeStore(string? source, string? link)
        {
            string s = (source ?? "").Trim().ToLowerInvariant();
            string host = "";
            if (!string.IsNullOrWhiteSpace(link) && Uri.TryCreate(link, UriKind.Absolute, out var uri))
                host = uri.Host.ToLowerInvariant();

            host = host.Replace("www.", "");
            var hay = $"{s} {host}";

            foreach (var (canonical, aliases) in StoreAliases)
            {
                foreach (var a in aliases)
                {
                    if (hay.Contains(a, StringComparison.OrdinalIgnoreCase))
                        return canonical;
                }
            }
            return null;
        }

        private async Task<List<decimal>> FromMercadoLivreUsed(string q, CancellationToken ct)
        {
            var url = $"https://api.mercadolibre.com/sites/MLB/search?q={Uri.EscapeDataString(q)}&limit=50";
            var cli = _http.CreateClient();

            var req = new HttpRequestMessage(HttpMethod.Get, url);
            // ajuda a evitar 403 em alguns ambientes
            req.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            var token = _cfg["ML_ACCESS_TOKEN"];
            if (!string.IsNullOrWhiteSpace(token))
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var outList = new List<decimal>();
            try
            {
                using var resp = await cli.SendAsync(req, ct);
                if (!resp.IsSuccessStatusCode) return outList;

                var json = await resp.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("results", out var arr)) return outList;

                foreach (var it in arr.EnumerateArray())
                {
                    var cond = it.TryGetProperty("condition", out var cd) ? cd.GetString() ?? "" : "";
                    if (!cond.Equals("used", StringComparison.OrdinalIgnoreCase)) continue;

                    var price = it.TryGetProperty("price", out var p) ? p.GetDecimal() : 0m;
                    var cur = it.TryGetProperty("currency_id", out var c) ? (c.GetString() ?? "BRL") : "BRL";
                    if (price > 0 && cur.Equals("BRL", StringComparison.OrdinalIgnoreCase))
                        outList.Add(price);
                }
            }
            catch { }

            return outList;
        }

        private async Task<List<decimal>> FromOlxViaSerpApi(string q, CancellationToken ct)
        {
            var key = _cfg["SERPAPI_KEY"];
            var outList = new List<decimal>();
            if (string.IsNullOrWhiteSpace(key)) return outList;

            var googleQuery = $"site:olx.com.br {q} R$";
            var url = $"https://serpapi.com/search?engine=google&q={Uri.EscapeDataString(googleQuery)}&gl=br&hl=pt-BR&num=50&api_key={Uri.EscapeDataString(key)}";

            var cli = _http.CreateClient();
            try
            {
                using var resp = await cli.GetAsync(url, ct);
                var json = await resp.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("organic_results", out var arr) || arr.ValueKind != JsonValueKind.Array)
                    return outList;

                foreach (var it in arr.EnumerateArray())
                {
                    var title = it.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                    var snippet = it.TryGetProperty("snippet", out var s) ? s.GetString() ?? "" : "";
                    var link = it.TryGetProperty("link", out var l) ? l.GetString() ?? "" : "";
                    var priceS = it.TryGetProperty("price", out var prStr) ? prStr.GetString() ?? "" : "";

                    if (!string.IsNullOrEmpty(link) && Uri.TryCreate(link, UriKind.Absolute, out var uri))
                    {
                        var host = uri.Host.ToLowerInvariant().Replace("www.", "");
                        if (!host.EndsWith("olx.com.br")) continue;
                    }

                    if (!string.IsNullOrEmpty(link) && !(link.Contains("/d/") || link.Contains("/item/")))
                        continue;

                    decimal? price = ExtractBrl(title) ?? ExtractBrl(snippet) ?? ExtractBrl(priceS);

                    if (!price.HasValue && it.TryGetProperty("rich_snippet", out var rs))
                    {
                        if (rs.TryGetProperty("top", out var top) && top.TryGetProperty("extensions", out var exts) && exts.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var ext in exts.EnumerateArray())
                            {
                                var sExt = ext.GetString() ?? "";
                                var p = ExtractBrl(sExt);
                                if (p.HasValue) { price = p; break; }
                            }
                        }
                    }

                    if (price.HasValue && price.Value > 0)
                        outList.Add(price.Value);
                }
            }
            catch {}

            return outList;

            static decimal? ExtractBrl(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return null;
                var m = Regex.Match(s, @"R\$ ?([\d\.]{1,3}(?:\.\d{3})*(?:,\d{2})|\d+)");
                if (!m.Success) return null;

                var num = m.Groups[1].Value.Trim().Replace(".", "").Replace(",", ".");
                return decimal.TryParse(num, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : (decimal?)null;
            }
        }

        private async Task<List<decimal>> FromEbayOptional(string q, CancellationToken ct)
        {
            var bearer = _cfg["EBAY_OAUTH_TOKEN"];
            if (string.IsNullOrWhiteSpace(bearer)) return new();

            var url = $"https://api.ebay.com/buy/browse/v1/item_summary/search?q={Uri.EscapeDataString(q)}&limit=50";
            var cli = _http.CreateClient();
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearer);

            var outList = new List<decimal>();
            try
            {
                using var resp = await cli.SendAsync(req, ct);
                if (!resp.IsSuccessStatusCode) return outList;

                var json = await resp.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("itemSummaries", out var arr)) return outList;

                var rates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { ["BRL"] = 1m };

                foreach (var it in arr.EnumerateArray())
                {
                    if (!it.TryGetProperty("price", out var pr)) continue;

                    decimal value = 0m;
                    if (pr.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.String)
                        value = decimal.Parse(v.GetString()!, System.Globalization.CultureInfo.InvariantCulture);
                    else if (pr.TryGetProperty("value", out var nv) && nv.ValueKind == JsonValueKind.Number)
                        value = nv.GetDecimal();

                    var currency = pr.TryGetProperty("currency", out var cu) ? (cu.GetString() ?? "USD") : "USD";
                    if (value <= 0) continue;

                    var brl = await ConvertToBrl(value, currency, rates, ct);
                    if (brl > 0) outList.Add(brl);
                }
            }
            catch { }

            return outList;
        }
        private static string BuildQuery(string categoria, string modelo)
        {
            string norm(string s) => Regex.Replace((s ?? "").Trim(), @"\s+", " ");
            var q = norm($"{modelo} {categoria}");
            q = Regex.Replace(q, @"\b(c[oó]digo|cod|sku)\s*[:#]?\s*\w+", "", RegexOptions.IgnoreCase);
            return q;
        }

        private static decimal AvgTrimmed(List<decimal> values)
        {
            var arr = values.Where(v => v > 0).OrderBy(v => v).ToArray();
            if (arr.Length == 0) return 0m;

            var med = Median(arr);
            var filtered = arr.Where(v => v >= med * 0.5m && v <= med * 2.0m).ToArray();
            if (filtered.Length == 0) filtered = arr;

            return Math.Round(filtered.Average(), 0);
        }

        private static decimal Median(decimal[] arr)
        {
            int n = arr.Length;
            return n % 2 == 1 ? arr[n / 2] : (arr[n / 2 - 1] + arr[n / 2]) / 2m;
        }

        private static decimal Round0(decimal v) => Math.Round(v, 0);

        private async Task<decimal> ConvertToBrl(decimal amount, string fromCurrency, Dictionary<string, decimal> cache, CancellationToken ct)
        {
            if (fromCurrency.Equals("BRL", StringComparison.OrdinalIgnoreCase)) return amount;
            if (!cache.TryGetValue(fromCurrency, out var rate))
            {
                var cli = _http.CreateClient();
                var url = $"https://api.exchangerate.host/convert?from={Uri.EscapeDataString(fromCurrency)}&to=BRL&amount=1";
                try
                {
                    var json = await cli.GetStringAsync(url, ct);
                    using var doc = JsonDocument.Parse(json);
                    rate = doc.RootElement.TryGetProperty("result", out var r) && r.ValueKind == JsonValueKind.Number ? r.GetDecimal() : 0m;
                }
                catch { rate = 0m; }
                cache[fromCurrency] = rate <= 0 ? 1m : rate;
            }
            var fx = cache[fromCurrency];
            return fx <= 0 ? amount : Math.Round(amount * fx, 0);
        }
    }
}
