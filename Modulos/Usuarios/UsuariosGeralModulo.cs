using System.Data;
using Microsoft.AspNetCore.Mvc;
using Orcei.Auxiliares;
using Orcei.Interfaces.Conexao;
using Orcei.Models.Conexao;
using Orcei.Models.Usuarios;
using Orcei.Modulos.Login;
using Orcei.ServicosConexao;
using Orcei.ServicosConexao.Services;

namespace Orcei.Modulos.Usuarios
{
    public class UsuariosGeralModulo : ConexaoModulo
    {
        public UsuariosGeralModulo()
        {
        }

        public UsuariosGeralModulo(IConexao dbContext) : base(dbContext)
        {
        }

        public List<RetornoUsuariosGeral> RetornaUsuariosGeral()
        {
            return DbContext.ObterLista<RetornoUsuariosGeral>($@"SELECT codigo_usuario CodigoUsuario,
                                                                        dta_registro DtaRegistro,
                                                                        nome_usuario NomeUsuario,
                                                                        email_usuario EmailUsuario
                                                                   FROM usuarios_geral");
        }

        public RetornoUsuariosGeral RetornaDadosUsuario(string email, IDbTransaction transaction = null)
        {
            const string sql = @"
            SELECT codigo_usuario AS CodigoUsuario,
                   dta_registro   AS DtaRegistro,
                   nome_usuario   AS NomeUsuario,
                   email_usuario  AS EmailUsuario
              FROM usuarios_geral
             WHERE lower(email_usuario) = :email";

            return DbContext.Obter<RetornoUsuariosGeral>(sql, new { email }, transaction);
        }

        public void InsereDadosUsuariosGeral(RegistrarUsuarioRequest request, IDbTransaction transaction = null)
        {
            var codigoUsuario = RetornaUsuariosGeral().Count + 1;

            string sql = @"INSERT INTO usuarios_geral (codigo_usuario, dta_registro, nome_usuario, email_usuario)
                           VALUES (:codigoUsuario, :dtaRegistro, :nomeUsuario, :emailUsuario)";

            DbContext.Execute(sql, new
            {
                codigoUsuario,
                dtaRegistro = DateTime.Now,
                nomeUsuario = request.NomeUsuario,
                emailUsuario = request.EmailUsuario
            }, transaction);
        }
    }
}
