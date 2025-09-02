namespace Orcei.Models.Usuarios
{
    public class SolicitarCodigoEmailModel
    {
        public string Email { get; set; }
    }

    public class GeraCodigoAutenticacaoResponse
    {
        public DateTime Validade { get; set; }
        public string CodigoGerado { get; set; }
    }

    public class RetornoDadoGeradoUsuarioResponse
    {
        public string Mensagem { get; set; }
        public DateTime Validade { get; set; }
        public RetornoUsuariosGeral DadosUsuario { get; set; }
    }

    public class PreLoginRequest
    {
        public string Email { get; set; }
    }

    public class CodigosGeradosResponse
    {
        public int IdCodigo { get; set; }
        public DateTime Expiracao { get; set; }
        public bool Utilizado { get; set; }
    }

    public class VerificaAcesso
    {
        public bool Status { get; set; }
        public bool Autenticado { get; set; }
    }

    public class LogoutModel
    {
        public bool Status { get; set; }
        public string Mensagem { get; set; }
    }
}
