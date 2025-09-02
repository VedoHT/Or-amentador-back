using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Orcei.Auxiliares;
using Orcei.Interfaces.Conexao;
using Orcei.Models.Conexao;
using Orcei.Models.Services;
using Orcei.Models.Usuarios;
using Orcei.Modulos.Usuarios;
using Orcei.ServicosConexao;
using Orcei.ServicosConexao.Services;

namespace Orcei.Modulos.Login
{
    public class LoginModulo : ConexaoModulo
    {
        public LoginModulo()
        {
        }

        public LoginModulo(IConexao dbContext) : base(dbContext)
        {
        }

        public GeraCodigoAutenticacaoResponse GeraCodigoAutenticacao(
            int codigoUsuario, string email, IDbTransaction tx = null)
        {
            var mod = new UsuariosCodigosModulo(DbContext);

            // Serializa concorrência por usuário
            DbContext.Execute("SELECT pg_advisory_xact_lock(:k);", new { k = codigoUsuario }, tx);

            var nowLocal = DateTime.Now;
            var validadeLocal = nowLocal.AddMinutes(2).AddSeconds(1);
            var emailNorm = (email ?? "").Trim().ToLowerInvariant();

            // Pega SEMPRE o último registro (sem filtro de data)
            var ultimo = mod.ObterUltimoPorUsuario(codigoUsuario, tx);

            // Decide em C# se está ativo
            bool estaUtilizado = ultimo?.Utilizado == true;
            bool estaAtivo = ultimo != null
                                 && !estaUtilizado
                                 && ultimo.Expiracao.ToLocalTime() > nowLocal;

            if (estaAtivo)
            {
                // Opcional: se faltar pouco, estende validade mantendo o MESMO código
                var restante = ultimo.Expiracao.ToLocalTime() - nowLocal;
                if (restante.TotalSeconds < 30)
                {
                    mod.AtualizarSomenteExpiracao(ultimo.IdCodigo, validadeLocal, tx);
                    ultimo.Expiracao = validadeLocal.ToUniversalTime();
                }

                return new GeraCodigoAutenticacaoResponse
                {
                    CodigoGerado = ultimo.CodigoGerado,
                    Validade = ultimo.Expiracao.ToLocalTime()
                };
            }

            // Não ativo: reaproveita a MESMA LINHA e troca código/validade
            var novoCodigo = FuncoesAuxiliares.GerarCodigo().ToString();

            if (ultimo != null)
            {
                mod.AtualizarCodigoReset(ultimo.IdCodigo, novoCodigo, validadeLocal, tx);

                return new GeraCodigoAutenticacaoResponse
                {
                    CodigoGerado = novoCodigo,
                    Validade = validadeLocal
                };
            }

            // Não havia linha -> cria a primeira
            mod.InsereDadosUsuariosCodigos(codigoUsuario, emailNorm, validadeLocal, novoCodigo, tx);

            return new GeraCodigoAutenticacaoResponse
            {
                CodigoGerado = novoCodigo,
                Validade = validadeLocal
            };
        }

        public RetornoDadoGeradoUsuarioResponse PreLogin(PreLoginRequest request)
        {
            var usuariosGeralModulo = new UsuariosGeralModulo(DbContext);
            var transaction = DbContext.IniciaTransacao();

            try
            {
                var emailNorm = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
                FuncoesAuxiliares.Validar(!EmailValido(emailNorm), MensagensPT.a0010, 412);

                var dadosUsuario = usuariosGeralModulo.RetornaDadosUsuario(emailNorm, transaction);
                FuncoesAuxiliares.Validar(dadosUsuario == null, MensagensPT.a0002, 412);

                var dadosGerados = new LoginModulo(DbContext)
                    .GeraCodigoAutenticacao(dadosUsuario.CodigoUsuario, emailNorm, transaction);

                var validadeFinal = dadosGerados.Validade == default
                    ? DateTime.Now.AddMinutes(2).AddSeconds(1)
                    : dadosGerados.Validade;

                DbContext.FazCommit(transaction);
                new EmailService().EnviarCodigoPorEmail(dadosUsuario.EmailUsuario, dadosGerados.CodigoGerado);

                return new RetornoDadoGeradoUsuarioResponse
                {
                    Mensagem = $"Código enviado para seu e-mail: {emailNorm}",
                    Validade = validadeFinal,
                    DadosUsuario = dadosUsuario
                };
            }
            catch (HaveException) { DbContext.FazRollback(transaction); throw; }
            catch (Exception ex) { DbContext.FazRollback(transaction); throw new HaveException(MensagensPT.b0001, 500, ex.Message); }
        }

        public bool RegistrarUsuario(RegistrarUsuarioRequest request)
        {
            UsuariosGeralModulo usuariosGeralModulo = new UsuariosGeralModulo(DbContext);

            FuncoesAuxiliares.Validar(string.IsNullOrEmpty(request.NomeUsuario), MensagensPT.a0011, 412);
            FuncoesAuxiliares.Validar(string.IsNullOrEmpty(request.EmailUsuario), MensagensPT.a0012, 412);
            FuncoesAuxiliares.Validar(!EmailValido(request.EmailUsuario), MensagensPT.a0010, 412);

            var transaction = DbContext.IniciaTransacao();

            try
            {
                var dadosUsuario = usuariosGeralModulo.RetornaDadosUsuario(request.EmailUsuario, transaction);
                FuncoesAuxiliares.Validar(dadosUsuario != null, MensagensPT.a0013, 412);

                usuariosGeralModulo.InsereDadosUsuariosGeral(request, transaction);

                DbContext.FazCommit(transaction);
                return true;
            }
            catch
            {
                DbContext.FazRollback(transaction);
                throw new HaveException("Ocorreu um erro ao registrar o usuário.", 412);
            }
        }

        public static bool EmailValido(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        public void ValidarCodigoAutenticacao(RetornoUsuariosGeral request, string codigo)
        {
            var usuariosCodigoModulo = new UsuariosCodigosModulo(DbContext);
            var tx = DbContext.IniciaTransacao();

            try
            {
                var emailNorm = (request.EmailUsuario ?? string.Empty).Trim().ToLowerInvariant();

                DbContext.Execute("SELECT pg_advisory_xact_lock(hashtext(:email));", new { email = emailNorm }, tx);

                usuariosCodigoModulo.ExcluirCodigosExpiradosPorEmail(emailNorm, tx);

                var result = usuariosCodigoModulo.RetornaIdCodigoExistente(request.CodigoUsuario, codigo, tx);

                FuncoesAuxiliares.Validar(result == null, MensagensPT.b0003, 412);
                FuncoesAuxiliares.Validar(result.Utilizado == true, MensagensPT.a0007, 412);

                var agoraLocal = DateTime.Now;
                FuncoesAuxiliares.Validar(result.Expiracao.ToLocalTime() <= agoraLocal, MensagensPT.a0008, 412);

                usuariosCodigoModulo.AlteraStatusValidado(result.IdCodigo, tx);
                usuariosCodigoModulo.ExcluirTodosPorEmailExceto(emailNorm, result.IdCodigo, tx);

                DbContext.FazCommit(tx);
            }
            catch (HaveException)
            {
                DbContext.FazRollback(tx);
                throw;
            }
            catch (Exception ex)
            {
                DbContext.FazRollback(tx);
                throw new HaveException(MensagensPT.b0004, 500, ex.Message);
            }
        }
    }
}
