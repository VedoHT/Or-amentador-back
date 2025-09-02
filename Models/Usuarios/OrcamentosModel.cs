namespace Orcei.Models.Usuarios
{
    public class CriarOrcamentoRequest
    {
        public string Categoria { get; set; } = default!;
        public string Modelo { get; set; } = default!;
        public int AnosUso { get; set; }
        public string Condicao { get; set; } = default!;
        public string Urgencia { get; set; } = default!;
        public bool TemCaixaManual { get; set; }
        public bool TemNotaFiscal { get; set; }
        public string? Observacoes { get; set; }
        public decimal? MediaNovo { get; set; }
        public decimal? MediaUsado { get; set; }
        public decimal? PrecoMin { get; set; }
        public decimal? PrecoMax { get; set; }
        public decimal? PrecoEscolhido { get; set; }
        public List<string>? Fotos { get; set; } = new();
        public long? UsuarioId { get; set; }
        public string? NomeCompleto { get; set; }
        public bool IncluirTransporte { get; set; }
        public decimal? ValorTransporteManual { get; set; }
        public decimal? ValorTransporteCalculado { get; set; }
    }

    public class CriarOrcamentoResponse
    {
        public long Id { get; set; }
        public string Slug { get; set; } = default!;
    }

    public class OrcamentoDetalhe
    {
        public long Id { get; set; }
        public string Slug { get; set; } = default!;
        public string Categoria { get; set; } = default!;
        public string Modelo { get; set; } = default!;
        public int AnosUso { get; set; }
        public string Condicao { get; set; } = default!;
        public string Urgencia { get; set; } = default!;
        public bool TemCaixaManual { get; set; }
        public bool TemNotaFiscal { get; set; }
        public string? Observacoes { get; set; }
        public decimal? MediaNovo { get; set; }
        public decimal? MediaUsado { get; set; }
        public decimal? PrecoMin { get; set; }
        public decimal? PrecoMax { get; set; }
        public decimal? PrecoEscolhido { get; set; }
        public string? NomeCompleto { get; set; }
        public bool IncluirTransporte { get; set; }
        public decimal? ValorTransporteManual { get; set; }
        public decimal? ValorTransporteCalculado { get; set; }
        public DateTime CriadoEm { get; set; }
        public List<string> Fotos { get; set; } = new();
    }

    public sealed class OrcamentoPublicoRow
    {
        public long Id { get; set; }
        public string Slug { get; set; } = default!;
        public string Categoria { get; set; } = default!;
        public string Modelo { get; set; } = default!;
        public int AnosUso { get; set; }
        public string Condicao { get; set; } = default!;
        public string Urgencia { get; set; } = default!;
        public bool TemCaixaManual { get; set; }
        public bool TemNotaFiscal { get; set; }
        public string? Observacoes { get; set; }
        public decimal? MediaNovo { get; set; }
        public decimal? MediaUsado { get; set; }
        public decimal? PrecoMin { get; set; }
        public decimal? PrecoMax { get; set; }
        public decimal? PrecoEscolhido { get; set; }

        public string? NomeCompleto { get; set; }
        public bool IncluirTransporte { get; set; }
        public decimal? ValorTransporteManual { get; set; }
        public decimal? ValorTransporteCalculado { get; set; }

        public DateTime CriadoEm { get; set; }
        public string? FotosJson { get; set; }
    }

    public class OrcamentoListaItem
    {
        public long Id { get; set; }
        public string Slug { get; set; } = default!;
        public string Titulo { get; set; } = default!;
        public DateTime CriadoEm { get; set; }
        public decimal? PrecoEscolhido { get; set; }
    }

    public class OrcamentoRow
    {
        public long Id { get; set; }
        public string Slug { get; set; } = "";
        public string Categoria { get; set; } = "";
        public string Modelo { get; set; } = "";
        public int AnosUso { get; set; }
        public string Condicao { get; set; } = "";
        public string Urgencia { get; set; } = "";
        public bool TemCaixaManual { get; set; }
        public bool TemNotaFiscal { get; set; }
        public string? Observacoes { get; set; }
        public decimal? MediaNovo { get; set; }
        public decimal? MediaUsado { get; set; }
        public decimal? PrecoMin { get; set; }
        public decimal? PrecoMax { get; set; }
        public decimal? PrecoEscolhido { get; set; }
        public string? FotosJson { get; set; } 
        public DateTime CriadoEm { get; set; }
        public string? EmailProprietario { get; set; }
    }

    public sealed record SiteAverage(string Site, decimal Valor, int Qtd);

    public sealed record CompararPrecosRequest
    {
        public string Categoria { get; set; } = "";
        public string Modelo { get; set; } = "";
    }

    public sealed record CompararPrecosResponse
    {
        public decimal? NewPriceAvg { get; init; }
        public decimal? UsedPriceAvg { get; init; }
        public decimal? PriceMin { get; init; }
        public decimal? PriceMax { get; init; }
        public List<SiteAverage> NewSites { get; init; } = new();
        public List<SiteAverage> UsedSites { get; init; } = new();
        public List<string> NewPriceSources { get; init; } = new();
        public List<string> UsedPriceSources { get; init; } = new();
    }
}
