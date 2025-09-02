using Orcei.Models.Usuarios;
using QuestPDF.Infrastructure;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using System.Globalization;
using System.Reflection.Metadata;
using Document = QuestPDF.Fluent.Document;
using Orcei.Models.Conexao;

namespace Orcei.Modulos.Orcamento
{
    public class GeradorPdfOrcamentoModulo
    {
        private static readonly CultureInfo PtBr = new CultureInfo("pt-BR");

        public byte[] GerarPDF(string slug)
        {
            var d = new OrcamentosModulo().ObterPublico(slug);
            if (d == null)
                throw new HaveException("Orçamento não encontrado.", 412);

            QuestPDF.Settings.License = LicenseType.Community;

            string C0(decimal? v) => v.HasValue ? v.Value.ToString("C0", PtBr) : "-";

            // transporte: manual vence calculado
            decimal? transp = null;
            if (d.IncluirTransporte)
                transp = (d.ValorTransporteManual.HasValue && d.ValorTransporteManual.Value > 0)
                    ? d.ValorTransporteManual
                    : d.ValorTransporteCalculado;

            string TransporteTexto()
            {
                if (!d.IncluirTransporte) return "Não";
                if (transp.HasValue) return C0(transp);
                return "Incluído";
            }

            // total (valor escolhido + transporte se houver)
            decimal? total = d.PrecoEscolhido.HasValue
                ? d.PrecoEscolhido.Value + (d.IncluirTransporte ? (transp ?? 0) : 0)
                : (decimal?)null;

            var fotos = (d.Fotos ?? new List<string>()).Take(5)
                .Select(ToBytes).Where(b => b?.Length > 0).ToList();

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(36);
                    page.DefaultTextStyle(ts => ts.FontSize(10));

                    // Cabeçalho com respiro
                    page.Header()
                        .Element(h => h.PaddingBottom(14))
                        .Row(row =>
                        {
                            row.RelativeItem().Column(col =>
                            {
                                col.Spacing(4);
                                col.Item().Text($"{d.Categoria} — {d.Modelo}")
                                          .FontSize(18).SemiBold();
                                col.Item().Text($"Orçamento • {d.CriadoEm:dd/MM/yyyy HH:mm}")
                                          .FontSize(10).FontColor(Colors.Grey.Darken2);
                            });

                            row.ConstantItem(130).AlignRight().Text(slug)
                               .FontSize(10).FontColor(Colors.Grey.Darken2);
                        });

                    page.Content().Column(col =>
                    {
                        col.Spacing(10);

                        // ===== META (respostas alinhadas) =====
                        col.Item()
                           .Border(1).BorderColor(Colors.Grey.Lighten3)
                           .Padding(8)
                           .Column(m =>
                           {
                               m.Spacing(2);

                               const float labelW = 120;

                               m.Item().Row(r =>
                               {
                                   r.ConstantItem(labelW).Text("Caixa manual:").FontColor(Colors.Grey.Darken2);
                                   r.RelativeItem().Text(d.TemCaixaManual ? "Sim" : "Não");
                               });

                               m.Item().Row(r =>
                               {
                                   r.ConstantItem(labelW).Text("Nota fiscal:").FontColor(Colors.Grey.Darken2);
                                   r.RelativeItem().Text(d.TemNotaFiscal ? "Sim" : "Não");
                               });

                               m.Item().Row(r =>
                               {
                                   r.ConstantItem(labelW).Text("Anos de uso:").FontColor(Colors.Grey.Darken2);
                                   r.RelativeItem().Text(d.AnosUso.ToString());
                               });

                               m.Item().Row(r =>
                               {
                                   r.ConstantItem(labelW).Text("Condição:").FontColor(Colors.Grey.Darken2);
                                   r.RelativeItem().Text(string.IsNullOrWhiteSpace(d.Condicao) ? "-" : d.Condicao);
                               });

                               m.Item().Row(r =>
                               {
                                   r.ConstantItem(labelW).Text("Urgência:").FontColor(Colors.Grey.Darken2);
                                   r.RelativeItem().Text(string.IsNullOrWhiteSpace(d.Urgencia) ? "-" : d.Urgencia);
                               });
                           });

                        // Cards do público: Nome completo, Valor escolhido, Transporte, Total
                        col.Item().Row(r =>
                        {
                            r.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten3).Padding(8).Column(c =>
                            {
                                c.Item().Text("Nome completo").FontColor(Colors.Grey.Darken2);
                                c.Item().Text(string.IsNullOrWhiteSpace(d.NomeCompleto) ? "-" : d.NomeCompleto)
                                        .FontSize(14).SemiBold();
                            });

                            r.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten3).Padding(8).Column(c =>
                            {
                                c.Item().Text("Valor escolhido").FontColor(Colors.Grey.Darken2);
                                c.Item().Text(C0(d.PrecoEscolhido)).FontSize(14).SemiBold();
                            });

                            r.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten3).Padding(8).Column(c =>
                            {
                                c.Item().Text("Transporte").FontColor(Colors.Grey.Darken2);
                                c.Item().Text(TransporteTexto()).FontSize(14).SemiBold();
                            });

                            r.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten3).Padding(8).Column(c =>
                            {
                                c.Item().Text("Total").FontColor(Colors.Grey.Darken2);
                                c.Item().Text(total.HasValue ? C0(total) : "-").FontSize(14).SemiBold();
                            });
                        });

                        // OBSERVAÇÕES — sempre mostrar
                        col.Item().Border(1).BorderColor(Colors.Grey.Lighten3).Padding(8).Column(obs =>
                        {
                            obs.Item().Text("Observações").SemiBold();
                            obs.Item().Container()
                                .PaddingTop(4)
                                .MinHeight(150)
                                .Text(d.Observacoes ?? string.Empty)
                                .FontSize(10);
                        });

                        // FOTOS — sempre mostrar
                        col.Item().Border(1).BorderColor(Colors.Grey.Lighten3).Padding(8).Column(f =>
                        {
                            f.Item().Text("Fotos").SemiBold();
                            f.Item().Container().PaddingTop(4).MinHeight(220).Element(e =>
                            {
                                if (fotos.Count > 0)
                                {
                                    e.Grid(grid =>
                                    {
                                        grid.Columns(3);
                                        foreach (var img in fotos)
                                            grid.Item().Height(140).Padding(4).Border(1)
                                                .BorderColor(Colors.Grey.Lighten3)
                                                .Image(img!).FitArea();
                                    });
                                }
                                else
                                {
                                    e.AlignMiddle().AlignCenter()
                                     .Text("Sem fotos").FontColor(Colors.Grey.Darken1);
                                }
                            });
                        });
                    });

                    page.Footer().AlignCenter()
                        .Text($"Gerado em {DateTime.Now:dd/MM/yyyy HH:mm} • {slug}")
                        .FontSize(9).FontColor(Colors.Grey.Darken2);
                });
            }).GeneratePdf();
        }

        private static byte[]? ToBytes(string? dataUrl)
        {
            if (string.IsNullOrWhiteSpace(dataUrl)) return null;
            var s = dataUrl;
            var comma = s.IndexOf(',');
            if (comma >= 0) s = s[(comma + 1)..];
            try { return Convert.FromBase64String(s); }
            catch { return null; }
        }
    }
}
