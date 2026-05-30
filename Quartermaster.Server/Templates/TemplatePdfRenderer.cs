using System.Threading.Tasks;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Quartermaster.Api.Templates;
using Quartermaster.Data.Templates;
using Quartermaster.Rendering;

namespace Quartermaster.Server.Templates;

public static class TemplatePdfRenderer {
    public static async Task<(byte[]? Pdf, string? Error)> RenderAsync(Template template) {
        var models = TemplateModelLookup.BuildForTemplate(
            template.Identifier, template.AllowsChapterFields, template.AllowsMemberFields, template.AllowsEventFields);
        var mockData = TemplateMockDataProvider.GetMockData(models);

        var (subjectText, subjectError) = await TemplateRenderer.RenderTextAsync(template.Subject ?? "", mockData);
        if (subjectError != null)
            return (null, subjectError);

        var (bodyText, bodyError, bodyContext) = await TemplateRenderer.RenderTextWithContextAsync(template.Body, mockData);
        if (bodyError != null)
            return (null, bodyError);

        var pdf = template.RenderMode switch {
            TemplateRenderMode.Envelope => TemplateEnvelopePdfRenderer.Render(
                EnvelopeTags.Extract(bodyContext!), bodyText ?? ""),
            _ => RenderSimple(subjectText ?? "", bodyText ?? "", template.DisplayName)
        };
        return (pdf, null);
    }

    private static byte[] RenderSimple(string subject, string body, string displayName) {
        var document = Document.Create(container => {
            container.Page(page => {
                page.Margin(40);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Helvetica"));

                page.Header().Element(h => ComposeHeader(h, subject, displayName));
                page.Content().Element(c => c.PaddingVertical(10).Column(col => {
                    col.Spacing(8);
                    MarkdownPdfRenderer.RenderInto(col, body);
                }));
                page.Footer().AlignCenter().Text(t => {
                    t.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Medium));
                    t.Span("Seite ");
                    t.CurrentPageNumber();
                    t.Span(" von ");
                    t.TotalPages();
                });
            });
        });
        return document.GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, string subject, string displayName) {
        container.PaddingBottom(16).Column(col => {
            var headline = string.IsNullOrWhiteSpace(subject) ? displayName : subject;
            col.Item().Text(headline).FontSize(20).Bold();
            col.Item().PaddingTop(8).LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
        });
    }
}
