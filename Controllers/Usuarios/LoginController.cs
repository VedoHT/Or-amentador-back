using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orcei.Models.Conexao;
using Orcei.Models.Usuarios;
using Orcei.Modulos.Login;

namespace Orcei.Controllers.Usuarios
{
    [Route("Login")]
    [ApiController]
    public class UsuariosController : ControllerBase
    {
        private readonly LoginModulo loginModulo = new LoginModulo();
        private readonly JwtTokenModulo jwtModulo;

        public UsuariosController(JwtTokenModulo jwtModuloRequest)
        {
            jwtModulo = jwtModuloRequest;
        }

        /// <summary>
        /// Endpoint que irá validar as condições do usuário e gerar o código para autenticar.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [AcceptVerbs("POST")]
        [Route("PreLogin")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(RetornoDadoGeradoUsuarioResponse))]
        [ProducesResponseType(StatusCodes.Status412PreconditionFailed, Type = typeof(HaveException))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(HaveException))]
        public ActionResult<RetornoDadoGeradoUsuarioResponse> PreLogin(PreLoginRequest request) 
        { 
            try 
            {
                return Ok(loginModulo.PreLogin(request));
            } 
            catch (HaveException e)
            { 
                var status = e.StatusCode > 0 ? e.StatusCode : 500; 
                
                var erro = new 
                { 
                    statusCode = status,
                    mensagem = e.Message,
                    data = e.DataExtra 
                };

                return StatusCode(status, erro);
            } 
        }

        /// <summary>
        /// Registra o usuário no banco de dados
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [AcceptVerbs("POST")]
        [Route("RegistrarUsuario")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(bool))]
        [ProducesResponseType(StatusCodes.Status412PreconditionFailed, Type = typeof(HaveException))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(HaveException))]
        public ActionResult<bool> RegistrarUsuario(RegistrarUsuarioRequest request)
        {
            try
            {
                return Ok(loginModulo.RegistrarUsuario(request));
            }
            catch (HaveException e)
            {
                var status = e.StatusCode > 0 ? e.StatusCode : 500;

                var erro = new
                {
                    statusCode = status,
                    mensagem = e.Message,
                    data = e.DataExtra
                };

                return StatusCode(status, erro);
            }
        }

        /// <summary>
        /// Recebe dados do usuário e gera o código jwt
        /// </summary>
        /// <param name="request"></param>
        /// <param name="codigo"></param>
        /// <param name="keepMeConnected"></param>
        /// <returns></returns>
        [AllowAnonymous]
        [AcceptVerbs("POST")]
        [Route("Login/{codigo}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(bool))]
        [ProducesResponseType(StatusCodes.Status412PreconditionFailed, Type = typeof(HaveException))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(HaveException))]
        public ActionResult<bool> Login(RetornoUsuariosGeral request, string codigo, bool keepMeConnected)
        {
            try
            {
                loginModulo.ValidarCodigoAutenticacao(request, codigo);

                var accessTokenOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.None,
                    Expires = keepMeConnected
                            ? DateTime.UtcNow.AddDays(7)
                            : DateTime.UtcNow.AddMinutes(25)
                };

                var refreshTokenOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.None,
                    Expires = keepMeConnected
                            ? DateTime.UtcNow.AddDays(30)
                            : DateTime.UtcNow.AddDays(7)
                };

                Response.Cookies.Append("jwt", jwtModulo.GerarTokenJwt(request), accessTokenOptions);
                Response.Cookies.Append("refreshToken", jwtModulo.GerarRefreshTokenJwt(request, keepMeConnected), refreshTokenOptions);
                return Ok(true);
            }
            catch (HaveException e)
            {
                var status = e.StatusCode > 0 ? e.StatusCode : 500;

                var erro = new
                {
                    statusCode = status,
                    mensagem = e.Message,
                    data = e.DataExtra
                };

                return StatusCode(status, erro);
            }
        }

        /// <summary>
        /// Deleta o JWT atual para realizar o logout
        /// </summary>
        /// <returns></returns>
        [AcceptVerbs("POST")]
        [Route("Logout")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(LogoutModel))]
        [ProducesResponseType(StatusCodes.Status412PreconditionFailed, Type = typeof(HaveException))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(HaveException))]
        public ActionResult<LogoutModel> Logout()
        {
            try
            {
                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTime.UtcNow.AddDays(-1)
                };

                Response.Cookies.Append("jwt", "", cookieOptions);
                Response.Cookies.Append("refreshToken", "", cookieOptions);
                return Ok(new LogoutModel { Status = true, Mensagem = "Logout realizado com sucesso." });
            }
            catch (HaveException e)
            {
                var status = e.StatusCode > 0 ? e.StatusCode : 500;

                var erro = new
                {
                    statusCode = status,
                    mensagem = e.Message,
                    data = e.DataExtra
                };

                return StatusCode(status, erro);
            }
        }
    }
}
