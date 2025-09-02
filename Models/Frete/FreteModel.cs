namespace Orcei.Models.Frete
{
    public class FreteEnderecoDto
    {
        public string Cep { get; set; } = default!;
        public string Uf { get; set; } = default!;
        public string Cidade { get; set; } = default!;
    }

    public class FretePackageDto
    {
        public int? Height { get; set; }
        public int? Width { get; set; }
        public int? Length { get; set; }
    }

    public class CalcularFreteRequest
    {
        public FreteEnderecoDto Origem { get; set; } = default!;
        public FreteEnderecoDto Destino { get; set; } = default!;
        public decimal PesoKg { get; set; }
        public bool RetornoVazio { get; set; } = false;
        public FretePackageDto? Package { get; set; }
    }

    public class CorreiosServicoItem
    {
        public string Codigo { get; set; } = default!;
        public string? Nome { get; set; }
        public decimal Valor { get; set; }
        public int? PrazoDias { get; set; }
        public string? Erro { get; set; }
    }

    public class CalcularFreteResponse
    {
        public decimal Valor { get; set; }
        public int? PrazoDias { get; set; }
        public List<CorreiosServicoItem> Servicos { get; set; } = new();
    }
}
