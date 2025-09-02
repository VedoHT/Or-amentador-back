using Orcei.Models.Frete;

namespace Orcei.Interfaces.Services
{
    public interface IFreteServiceInterface
    {
        Task<CalcularFreteResponse> CalcularFrete(CalcularFreteRequest req, CancellationToken ct = default);
    }
}
