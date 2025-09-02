using System.Data;
using Microsoft.EntityFrameworkCore;
using Orcei.Auxiliares;
using Orcei.Interfaces.Conexao;
using Orcei.Models.Conexao;
using Orcei.Models.Usuarios;
using Orcei.ServicosConexao;

namespace Orcei.Modulos.Usuarios
{
    public class UsuariosCodigosModulo : ConexaoModulo
    {
        public UsuariosCodigosModulo()
        {
        }

        public UsuariosCodigosModulo(IConexao dbContext) : base(dbContext)
        {
        }

        public int RetornaProximoIdCodigoAuth(IDbTransaction transaction = null)
        {
            var dadoAtual = DbContext.ObterScalar<int>(@"select max(id_codigo) from usuarios_codigos", null, transaction);

            if (dadoAtual == 0)
                dadoAtual = 0;

            return dadoAtual += 1;
        }

        public void InsereDadosUsuariosCodigos(int codigoUsuario, string emailUsuario, DateTime validadeLocal, string codigoGerado, IDbTransaction tx = null)
        {
            var validadeUtc = validadeLocal.ToUniversalTime();
            DbContext.Execute(@"
            INSERT INTO usuarios_codigos (id_codigo, codigo_usuario, email_usuario, codigo_gerado, expiracao)
            VALUES (:idCodigo, :codigoUsuario, :emailUsuario, :codigoGerado, :expiracao)",
                new
                {
                    idCodigo = RetornaProximoIdCodigoAuth(tx),
                    codigoUsuario,
                    emailUsuario = Normalize(emailUsuario),
                    codigoGerado,
                    expiracao = validadeUtc
                }, tx);
        }

        public CodigoUsuarioUnico ObterUltimoPorUsuario(int codigoUsuario, IDbTransaction tx = null)
        {
            const string sql = @"
                SELECT id_codigo      AS IdCodigo,
                       codigo_gerado  AS CodigoGerado,
                       expiracao      AS Expiracao,
                       utilizado      AS Utilizado,
                       email_usuario  AS EmailUsuario
                  FROM usuarios_codigos
                 WHERE codigo_usuario = :u
                 ORDER BY id_codigo DESC
                 LIMIT 1;";
            return DbContext.Obter<CodigoUsuarioUnico>(sql, new { u = codigoUsuario }, tx);
        }

        public CodigosGeradosResponse RetornaIdCodigoExistente(int codigousuario, string codigo, IDbTransaction transaction = null)
        {
            var sql = @"select id_codigo IdCodigo,
                               expiracao Expiracao,
                               utilizado Utilizado
                          from usuarios_codigos
                         where codigo_gerado = :codigo
                           and codigo_usuario = :codigousuario";

            return DbContext.Obter<CodigosGeradosResponse>(sql, new { codigousuario, codigo }, transaction);
        }

        public void AlteraStatusValidado(int idCodigo, IDbTransaction transaction = null)
        {
            DbContext.Execute("update usuarios_codigos set utilizado = true where id_codigo = :idCodigo", new { idCodigo }, transaction);
        }

        public int ExcluirCodigosExpiradosPorEmail(string email, IDbTransaction tx = null)
        {
            var nowUtc = DateTime.Now.ToUniversalTime();
            const string sql = @"
            DELETE FROM usuarios_codigos
             WHERE email_usuario = :email
               AND expiracao <= :now;";
            return DbContext.Execute(sql, new { email = Normalize(email), now = nowUtc }, tx);
        }

        public int ExcluirTodosPorEmailExceto(string email, int idCodigoManter, IDbTransaction tx = null)
        {
            const string sql = @"
            DELETE FROM usuarios_codigos
             WHERE email_usuario = :email
               AND id_codigo <> :id;";
            return DbContext.Execute(sql, new { email = Normalize(email), id = idCodigoManter }, tx);
        }

        public void AtualizarSomenteExpiracao(int idCodigo, DateTime novaValidadeLocal, IDbTransaction tx = null)
        {
            var novaUtc = novaValidadeLocal.ToUniversalTime();
            const string sql = @"UPDATE usuarios_codigos SET expiracao = :nova WHERE id_codigo = :id;";
            DbContext.Execute(sql, new { id = idCodigo, nova = novaUtc }, tx);
        }

        public void AtualizarCodigoReset(int idCodigo, string novoCodigo, DateTime novaValidadeLocal, IDbTransaction tx = null)
        {
            var novaUtc = novaValidadeLocal.ToUniversalTime();
            const string sql = @"
            UPDATE usuarios_codigos
               SET codigo_gerado = :codigo,
                   expiracao     = :nova,
                   utilizado     = FALSE
             WHERE id_codigo     = :id;";
            DbContext.Execute(sql, new { id = idCodigo, codigo = novoCodigo, nova = novaUtc }, tx);
        }

        private static string Normalize(string s) => (s ?? "").Trim().ToLowerInvariant();
    }
}
