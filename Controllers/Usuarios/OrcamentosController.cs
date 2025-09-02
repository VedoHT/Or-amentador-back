using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Orcei.Interfaces.Services;
using Orcei.Models.Conexao;
using Orcei.Models.Frete;
using Orcei.Models.Usuarios;
using Orcei.Modulos.Orcamento;
using Orcei.ServicosConexao.Services;

namespace Orcei.Controllers.Orcamentos
{
    [Route("Orcamentos")]
    [ApiController]
    public class OrcamentosController : ControllerBase
    {
        private readonly OrcamentosModulo orcModulo = new OrcamentosModulo();
        private readonly GeradorPdfOrcamentoModulo pdfModulo = new GeradorPdfOrcamentoModulo();
        private readonly IFreteServiceInterface _svc;
        private readonly ComparadorService _priceCompare;
        public OrcamentosController(IFreteServiceInterface svc, ComparadorService priceCompare)
        {
            _svc = svc;
            _priceCompare = priceCompare;
        }


        /// <summary>
        /// Cria um orçamento. Permite anônimo; se o usuário estiver autenticado, vincula ao usuário do JWT.
        /// </summary>
        /// <param name="request">Dados do orçamento</param>
        [AcceptVerbs("POST")]
        [Route("Criar")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CriarOrcamentoResponse))]
        [ProducesResponseType(StatusCodes.Status412PreconditionFailed, Type = typeof(HaveException))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(HaveException))]
        public ActionResult<CriarOrcamentoResponse> Criar(CriarOrcamentoRequest request)
        {
            try
            {
                var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (long.TryParse(idClaim, out var uid))
                {
                    if (request.UsuarioId.HasValue && request.UsuarioId.Value != uid)
                        throw new HaveException("Usuário inconsistente na requisição.", 412);
                }

                var qtd = request.Fotos?.Count ?? 0;
                var head = (qtd > 0 ? request.Fotos[0] : "");
                Console.WriteLine($"[Criar] Fotos={qtd} first={head?.Substring(0, Math.Min(60, head.Length))}");

                return Ok(orcModulo.Criar(request));
            }
            catch (HaveException e)
            {
                var status = e.StatusCode > 0 ? e.StatusCode : 500;
                var erro = new { statusCode = status, mensagem = e.Message, data = e.DataExtra };
                return StatusCode(status, erro);
            }
        }

        /// <summary>
        /// Retorna um PDF do orçamento selecionado.
        /// </summary>
        /// <param name="slug">Identificador legível do orçamento</param>
        [AcceptVerbs("GET")]
        [Route("GerarPDF/{slug}")]
        [Produces("application/pdf")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(OrcamentoDetalhe))]
        [ProducesResponseType(StatusCodes.Status412PreconditionFailed, Type = typeof(HaveException))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(HaveException))]
        public IActionResult GerarPDF(string slug)
        {
            try
            {
                var bytes = pdfModulo.GerarPDF(slug);
                var fileName = $"{slug}.pdf";
                return File(bytes, "application/pdf", fileName);
            }
            catch (HaveException e)
            {
                var status = e.StatusCode > 0 ? e.StatusCode : 500;
                var erro = new { statusCode = status, mensagem = e.Message, data = e.DataExtra };
                return StatusCode(status, erro);
            }
        }

        /// <summary>
        /// Retorna valores do frete calculado.
        /// </summary>
        /// <param name="req">Identificador legível do orçamento</param>
        [AcceptVerbs("POST")]
        [Route("CalcularFrete")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(OrcamentoDetalhe))]
        [ProducesResponseType(StatusCodes.Status412PreconditionFailed, Type = typeof(HaveException))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(HaveException))]
        public async Task<ActionResult> CalcularFrete(CalcularFreteRequest req)
        {
            try
            {
                if (req?.Origem == null || req.Destino == null)
                    return BadRequest(new { mensagem = "Origem e destino são obrigatórios." });

                var resp = await _svc.CalcularFrete(req);

                return Ok(new
                {
                    valor = resp.Valor,
                    prazoDias = resp.PrazoDias,
                    servicos = resp.Servicos.Select(s => new {
                        codigo = s.Codigo,
                        nome = s.Nome,
                        valor = s.Valor,
                        prazoDias = s.PrazoDias,
                        erro = s.Erro
                    }).ToList()
                });
            }
            catch (HaveException e)
            {
                var status = e.StatusCode > 0 ? e.StatusCode : 500;
                return StatusCode(status, new { statusCode = status, mensagem = e.Message, data = e.DataExtra });
            }
        }

        /// <summary>
        /// Retorna um orçamento público pelo slug.
        /// </summary>
        /// <param name="slug">Identificador legível do orçamento</param>
        [AcceptVerbs("GET")]
        [Route("Publico/{slug}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(OrcamentoDetalhe))]
        [ProducesResponseType(StatusCodes.Status412PreconditionFailed, Type = typeof(HaveException))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(HaveException))]
        public ActionResult<OrcamentoDetalhe> Publico(string slug)
        {
            try
            {
                var dado = orcModulo.ObterPublico(slug);
                return Ok(dado);
            }
            catch (HaveException e)
            {
                var status = e.StatusCode > 0 ? e.StatusCode : 500;
                var erro = new { statusCode = status, mensagem = e.Message, data = e.DataExtra };
                return StatusCode(status, erro);
            }
        }

        /// <summary>
        /// Lista orçamentos do usuário autenticado.
        /// </summary>
        [AcceptVerbs("GET")]
        [Route("Meus")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<OrcamentoListaItem>))]
        [ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(HaveException))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(HaveException))]
        public ActionResult<List<OrcamentoListaItem>> Meus()
        {
            try
            {
                var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!long.TryParse(idClaim, out var codigoUsuario))
                    throw new HaveException("Não autorizado.", 401);

                var lista = orcModulo.ListarMeus(codigoUsuario);
                return Ok(lista);
            }
            catch (HaveException e)
            {
                var status = e.StatusCode > 0 ? e.StatusCode : 500;
                var erro = new { statusCode = status, mensagem = e.Message, data = e.DataExtra };
                return StatusCode(status, erro);
            }
        }

        /// <summary>
        /// Retorna médias por site (novos/usados) para o termo informado.
        /// </summary>
        [AcceptVerbs("POST")]
        [Route("CompararPrecos")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CompararPrecosResponse))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(HaveException))]
        public async Task<ActionResult> CompararPrecos(CompararPrecosRequest req)
        {
            try
            {
                var modelo = (req?.Modelo ?? "").Trim();
                if (modelo.Length < 2) return BadRequest(new { mensagem = "Informe o modelo." });

                var result = await _priceCompare.CompareAsync(req?.Categoria ?? "", modelo);
                return Ok(result);
            }
            catch (HaveException e)
            {
                var status = e.StatusCode > 0 ? e.StatusCode : 500;
                return StatusCode(status, new { statusCode = status, mensagem = e.Message, data = e.DataExtra });
            }
        }
    }
}
