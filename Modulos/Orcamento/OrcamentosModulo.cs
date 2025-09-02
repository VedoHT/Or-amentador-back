using System.Data;
using System.Text.Json;
using Microsoft.IdentityModel.Logging;
using Orcei.Auxiliares;
using Orcei.Interfaces.Conexao;
using Orcei.Models.Conexao;
using Orcei.Models.Usuarios;
using Orcei.ServicosConexao;

namespace Orcei.Modulos.Orcamento
{
    public class OrcamentosModulo : ConexaoModulo
    {
        public OrcamentosModulo() { }
        public OrcamentosModulo(IConexao dbContext) : base(dbContext) { }

        public long RetornaProximoIdOrcamento(IDbTransaction transaction = null)
        {
            var atual = DbContext.ObterScalar<long>(@"select coalesce(max(id_orcamento), 0) from public.orcamentos", null, transaction);
            if (atual == 0) atual = 0;
            return atual + 1;
        }

        public bool SlugExiste(string slug, IDbTransaction transaction = null)
        {
            var existe = DbContext.ObterScalar<int>(@"select 1 from public.orcamentos where slug = :slug limit 1", new { slug }, transaction);
            return existe == 1;
        }

        public string GerarSlugUnico(string categoria, string modelo, IDbTransaction transaction = null)
        {
            var baseSlug = FuncoesAuxiliares.ToSlug($"{categoria}-{modelo}");
            var slug = baseSlug;
            var tentativas = 0;

            while (SlugExiste(slug, transaction))
            {
                tentativas++;
                var suf = Guid.NewGuid().ToString("n")[..5];
                slug = $"{baseSlug}-{suf}";
                if (tentativas > 20) break;
            }

            return slug;
        }

        public CriarOrcamentoResponse Criar(CriarOrcamentoRequest req, IDbTransaction transaction = null)
        {
            long? codigoUsuario = req.UsuarioId;

            var novoId = RetornaProximoIdOrcamento(transaction);
            var slug = GerarSlugUnico(req.Categoria, req.Modelo, transaction);

            var fotosJson = (req.Fotos != null && req.Fotos.Any())
                ? JsonSerializer.Serialize(req.Fotos)
                : "[]";

            const string sql = @"
                insert into public.orcamentos
                ( id_orcamento, slug, categoria, modelo, anos_uso, condicao, urgencia,
                  tem_caixa_manual, tem_nota_fiscal, observacoes,
                  media_novo, media_usado, preco_min, preco_max, preco_escolhido,
                  fotos, codigo_usuario, criado_em, atualizado_em,
                  nome_completo, transporte_incluido, valor_transporte_manual, valor_transporte_calculado
                )
                values
                ( :id, :slug, :categoria, :modelo, :anos, :condicao, :urgencia,
                  :caixa, :nf, :obs,
                  :mn, :mu, :pmin, :pmax, :pescolhido,
                  cast(:fotos as jsonb), :user, :criadoEm, :atualizadoEm,
                  :nomeCompleto, :transpIncluido, :valorTranspManual, :valorTranspCalculado
                );";

            DbContext.Execute(sql, new
            {
                id = novoId,
                slug,
                categoria = req.Categoria,
                modelo = req.Modelo,
                anos = req.AnosUso,
                condicao = req.Condicao,
                urgencia = req.Urgencia,
                caixa = req.TemCaixaManual,
                nf = req.TemNotaFiscal,
                obs = (object?)req.Observacoes ?? DBNull.Value,
                mn = (object?)req.MediaNovo ?? DBNull.Value,
                mu = (object?)req.MediaUsado ?? DBNull.Value,
                pmin = (object?)req.PrecoMin ?? DBNull.Value,
                pmax = (object?)req.PrecoMax ?? DBNull.Value,
                pescolhido = (object?)req.PrecoEscolhido ?? DBNull.Value,

                fotos = fotosJson,
                user = (object?)codigoUsuario ?? DBNull.Value,
                criadoEm = DateTime.Now,         // mantém como estava no teu código
                atualizadoEm = DateTime.Now,

                // 👇 novos campos (etapa 9)
                nomeCompleto = (object?)req.NomeCompleto ?? DBNull.Value,
                transpIncluido = req.IncluirTransporte,
                valorTranspManual = (object?)req.ValorTransporteManual ?? DBNull.Value,
                valorTranspCalculado = (object?)req.ValorTransporteCalculado ?? DBNull.Value
            }, transaction);

            return new CriarOrcamentoResponse
            {
                Id = novoId,
                Slug = slug
            };
        }

        public OrcamentoDetalhe ObterPublico(string slug, IDbTransaction transaction = null)
        {
            const string sql = @"
            select  o.id_orcamento               as Id,
                    o.slug                       as Slug,
                    o.categoria                  as Categoria,
                    o.modelo                     as Modelo,
                    o.anos_uso                   as AnosUso,
                    o.condicao                   as Condicao,
                    o.urgencia                   as Urgencia,
                    o.tem_caixa_manual           as TemCaixaManual,
                    o.tem_nota_fiscal            as TemNotaFiscal,
                    o.observacoes                as Observacoes,
                    o.media_novo                 as MediaNovo,
                    o.media_usado                as MediaUsado,
                    o.preco_min                  as PrecoMin,
                    o.preco_max                  as PrecoMax,
                    o.preco_escolhido            as PrecoEscolhido,
                    o.nome_completo              as NomeCompleto,
                    o.transporte_incluido        as IncluirTransporte,
                    o.valor_transporte_manual    as ValorTransporteManual,
                    o.valor_transporte_calculado as ValorTransporteCalculado,
                    o.criado_em                  as CriadoEm,
                    o.fotos::text                as FotosJson
            from public.orcamentos o
            where o.slug = :slug
            limit 1;";

            var row = DbContext.Obter<OrcamentoPublicoRow>(sql, new { slug }, transaction);
            if (row == null) return null;

            var detalhe = new OrcamentoDetalhe
            {
                Id = row.Id,
                Slug = row.Slug,
                Categoria = row.Categoria,
                Modelo = row.Modelo,
                AnosUso = row.AnosUso,
                Condicao = row.Condicao,
                Urgencia = row.Urgencia,
                TemCaixaManual = row.TemCaixaManual,
                TemNotaFiscal = row.TemNotaFiscal,
                Observacoes = row.Observacoes,

                MediaNovo = row.MediaNovo,
                MediaUsado = row.MediaUsado,
                PrecoMin = row.PrecoMin,
                PrecoMax = row.PrecoMax,
                PrecoEscolhido = row.PrecoEscolhido,

                NomeCompleto = row.NomeCompleto,
                IncluirTransporte = row.IncluirTransporte,
                ValorTransporteManual = row.ValorTransporteManual,
                ValorTransporteCalculado = row.ValorTransporteCalculado,

                CriadoEm = row.CriadoEm,
                Fotos = ParseFotos(row.FotosJson)
            };

            return detalhe;
        }

        public List<OrcamentoListaItem> ListarMeus(long codigoUsuario, IDbTransaction transaction = null)
        {
            var sql = @"
                select  id_orcamento                                    as Id,
                        slug                                            as Slug,
                        (categoria || ' • ' || modelo)                  as Titulo,
                        criado_em                                       as CriadoEm,
                        preco_escolhido                                 as PrecoEscolhido
                  from public.orcamentos
                 where codigo_usuario = :u
              order by criado_em desc
                 limit 200
            ";

            var lista = new List<OrcamentoListaItem>();
            var rows = DbContext.ObterLista<OrcamentoListaItem>(sql, new { u = codigoUsuario }, transaction);

            foreach (var r in rows)
            {
                lista.Add(new OrcamentoListaItem
                {
                    Id = r.Id,
                    Slug = r.Slug,
                    Titulo = r.Titulo,
                    CriadoEm = r.CriadoEm,
                    PrecoEscolhido = r.PrecoEscolhido
                });
            }

            return lista;
        }

        private static List<string> ParseFotos(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<string>();
            List<string>? list;
            try { list = JsonSerializer.Deserialize<List<string>>(json); }
            catch { list = new List<string>(); }
            list ??= new List<string>();
            for (int i = 0; i < list.Count; i++)
                if (!string.IsNullOrEmpty(list[i]) && !list[i].Contains("base64,", StringComparison.OrdinalIgnoreCase))
                    list[i] = "data:image/jpeg;base64," + list[i];
            return list;
        }
    }
}
