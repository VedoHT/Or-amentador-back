using System.Linq;
using Orcei.Interfaces.Services;
using Orcei.Models.Conexao;
using Orcei.Models.Frete;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Orcei.ServicosConexao.Services
{
    public class FreteService : IFreteServiceInterface
    {
        private readonly IHttpClientFactory _http;
        private readonly IConfiguration _cfg;

        public FreteService(IHttpClientFactory http, IConfiguration cfg)
        {
            _http = http;
            _cfg = cfg;
        }

        public async Task<CalcularFreteResponse> CalcularFrete(CalcularFreteRequest req, CancellationToken ct = default)
        {
            if (req?.Origem == null || req.Destino == null)
                throw new HaveException("Origem e destino são obrigatórios.", 400);

            // --------------------
            // 1) Token & headers
            // --------------------
            var tokenCfg = (_cfg["SuperFrete:Bearer"] ?? "").Trim();
            if (tokenCfg.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                tokenCfg = tokenCfg.Substring(7).Trim();
            if (string.IsNullOrWhiteSpace(tokenCfg))
                throw new HaveException("Token SuperFrete não configurado.", 500);

            var userAgent = _cfg["SuperFrete:UserAgent"] ?? "Orcei/1.0 ([email protected])";
            var servicesStr = (_cfg["SuperFrete:Services"] ?? "1,2").Trim(); // 1=PAC, 2=SEDEX (padrão)

            var client = _http.CreateClient("superfrete"); // BaseAddress vem do Program.cs (sandbox/prod)

            // (opcional) Smoke test do token
            if (_cfg.GetValue<bool?>("SuperFrete:SmokeTest", true) == true)
            {
                using var smoke = new HttpRequestMessage(HttpMethod.Get, "api/v0/user");
                smoke.Headers.Accept.ParseAdd("application/json");
                if (!smoke.Headers.TryAddWithoutValidation("User-Agent", userAgent))
                    smoke.Headers.UserAgent.ParseAdd(userAgent);
                smoke.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenCfg);

                using var chk = await client.SendAsync(smoke, HttpCompletionOption.ResponseHeadersRead, ct);
                var chkBody = await chk.Content.ReadAsStringAsync(ct);
                if (!chk.IsSuccessStatusCode)
                    throw new HaveException($"Token inválido ou ambiente incorreto ({(int)chk.StatusCode}). Detalhe: {chkBody}", 401);
            }

            // ---------------------------------
            // 2) Normaliza peso e dimensões
            // ---------------------------------
            var peso = req.PesoKg <= 0 ? 1m : req.PesoKg;
            peso = Math.Clamp(peso, 0.3m, 120m); // SuperFrete permite >30kg em algumas transportadoras

            var defPkg = _cfg.GetSection("SuperFrete:DefaultPackage");
            var defH = defPkg.GetValue<int?>("height") ?? 5;
            var defW = defPkg.GetValue<int?>("width") ?? 15;
            var defL = defPkg.GetValue<int?>("length") ?? 20;

            // pega do req.Package se vier (>0), senão fica com defaults
            int height = PickPositiveOrDefault(req.Package?.Height, defH);
            int width = PickPositiveOrDefault(req.Package?.Width, defW);
            int length = PickPositiveOrDefault(req.Package?.Length, defL);

            // ---------------------------------
            // 3) Monta request de cálculo (POST)
            // ---------------------------------
            var bodyObj = new
            {
                from = new { postal_code = SomenteDigitos(req.Origem.Cep) },
                to = new { postal_code = SomenteDigitos(req.Destino.Cep) },
                services = servicesStr,
                options = new { own_hand = false, receipt = false, insurance_value = 0, use_insurance_value = false },
                package = new { height, width, length, weight = peso }
            };

            var jsonBody = JsonSerializer.Serialize(bodyObj);

            using var msg = new HttpRequestMessage(HttpMethod.Post, "api/v0/calculator");
            msg.Headers.Accept.ParseAdd("application/json");
            if (!msg.Headers.TryAddWithoutValidation("User-Agent", userAgent))
                msg.Headers.UserAgent.ParseAdd(userAgent);
            msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenCfg);
            msg.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            using var resp = await client.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                string msgErr = "Falha ao consultar SuperFrete.";
                try
                {
                    using var errDoc = JsonDocument.Parse(json);
                    if (errDoc.RootElement.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
                        msgErr = m.GetString() ?? msgErr;
                }
                catch { }
                throw new HaveException($"{msgErr} ({(int)resp.StatusCode}). Detalhe: {json}", (int)resp.StatusCode);
            }

            // --------------------
            // 4) Parse do retorno
            // --------------------
            using var doc = JsonDocument.Parse(json);

            IEnumerable<JsonElement> items;
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                items = doc.RootElement.EnumerateArray();
            else if (doc.RootElement.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Array)
                items = dataEl.EnumerateArray();
            else
                items = Array.Empty<JsonElement>();

            var lista = new List<CorreiosServicoItem>();

            foreach (var item in items)
            {
                string codigo = "";
                string? nome = null;
                if (item.TryGetProperty("service", out var svc) && svc.ValueKind == JsonValueKind.Object)
                {
                    if (svc.TryGetProperty("id", out var idEl))
                        codigo = idEl.ValueKind == JsonValueKind.String ? (idEl.GetString() ?? "") : idEl.GetRawText().Trim('"');
                    if (svc.TryGetProperty("name", out var nmEl) && nmEl.ValueKind == JsonValueKind.String)
                        nome = nmEl.GetString();
                }

                var valor = TryGetDecimalFlexible(item, "custom_price")
                            ?? TryGetDecimalFlexible(item, "price")
                            ?? 0m;

                int? prazo = TryGetIntFlexible(item, "custom_delivery_time")
                             ?? TryGetIntFlexible(item, "delivery_time");

                string? erro = null;
                if (item.TryGetProperty("error", out var errEl) && errEl.ValueKind != JsonValueKind.Null && errEl.ValueKind != JsonValueKind.False)
                {
                    if (errEl.ValueKind == JsonValueKind.String)
                        erro = errEl.GetString();
                    else if (errEl.ValueKind == JsonValueKind.Object)
                    {
                        if (errEl.TryGetProperty("message", out var em) && em.ValueKind == JsonValueKind.String)
                            erro = em.GetString();
                        else
                            erro = errEl.ToString();
                    }
                    else
                        erro = errEl.ToString();
                }

                lista.Add(new CorreiosServicoItem
                {
                    Codigo = codigo,
                    Nome = nome,
                    Valor = valor,
                    PrazoDias = prazo,
                    Erro = erro
                });
            }

            // 5) Filtra serviços inválidos
            var disponiveis = lista
                .Where(x => x.Erro == null && x.Valor > 0m && x.PrazoDias.HasValue && x.PrazoDias.Value > 0)
                .ToList();

            var melhor = disponiveis.OrderBy(x => x.Valor).FirstOrDefault();
            if (melhor == null)
                throw new HaveException("Nenhum serviço disponível para os parâmetros informados.", 412);

            return new CalcularFreteResponse
            {
                Valor = melhor.Valor,
                PrazoDias = melhor.PrazoDias,
                Servicos = disponiveis
            };
        }

        private static string SomenteDigitos(string s) => new string((s ?? "").Where(char.IsDigit).ToArray());

        private static int PickPositiveOrDefault(int? v, int def)
            => (v.HasValue && v.Value > 0) ? v.Value : def;

        private static decimal? TryGetDecimalFlexible(JsonElement el, string prop)
        {
            if (!el.TryGetProperty(prop, out var p)) return null;
            switch (p.ValueKind)
            {
                case JsonValueKind.Number:
                    return p.GetDecimal();
                case JsonValueKind.String:
                    var txt = p.GetString();
                    if (string.IsNullOrWhiteSpace(txt)) return null;
                    if (decimal.TryParse(txt, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var inv))
                        return inv;
                    if (decimal.TryParse(txt, System.Globalization.NumberStyles.Any, new System.Globalization.CultureInfo("pt-BR"), out var pt))
                        return pt;
                    return null;
                default:
                    return null;
            }
        }

        private static int? TryGetIntFlexible(JsonElement el, string prop)
        {
            if (!el.TryGetProperty(prop, out var p)) return null;
            return p.ValueKind == JsonValueKind.Number ? p.GetInt32()
                 : (p.ValueKind == JsonValueKind.String && int.TryParse(p.GetString(), out var n) ? n : (int?)null);
        }
    }
}
