using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Orcei.Models.Usuarios
{
    public class RetornoUsuariosGeral
    {
        [JsonIgnore]
        public int CodigoUsuario { get; set; }
        public DateTime DtaRegistro { get; set; }
        public string NomeUsuario { get; set; }
        [JsonIgnore]
        public string EmailUsuario { get; set; }
    }

    public class RetornaDadosAlteracaoUsuario
    {
        public string Mensagem { get; set; }
        public DateTime Validade { get; set; }
        public RetornoUsuariosGeral DadosUsuario { get; set; }
    }

    public class RegistrarUsuarioRequest
    {
        public string NomeUsuario { get; set; }
        public string EmailUsuario { get; set; }
    }

    public class UsuarioCodigoAtivo
    {
        public int IdCodigo { get; set; }
        public string CodigoGerado { get; set; }
        public DateTime Expiracao { get; set; }
    }

    public sealed class CodigoUsuarioUnico
    {
        public int IdCodigo { get; set; }
        public string CodigoGerado { get; set; }
        public DateTime Expiracao { get; set; } // Npgsql lê em UTC
        public bool? Utilizado { get; set; }    // pode vir null
        public string EmailUsuario { get; set; }
    }
}
