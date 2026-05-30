using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Quartermaster.Rendering;

namespace Quartermaster.Server.Templates;

public static class TemplateEnvelopePdfRenderer {
    private const float SenderTopMm = 45f;
    private const float SenderToRecipientGapMm = 2f;
    private const float AddressLeftMm = 20f;
    private const float BodyTopGapMm = 20f;
    private const float BodyHorizontalMarginMm = 25f;
    private const float PageBottomMarginMm = 20f;

    public static byte[] Render(EnvelopeData envelope, string body) {
        var document = Document.Create(container => {
            container.Page(page => {
                page.Size(PageSizes.A4);
                page.MarginTop(0, Unit.Millimetre);
                page.MarginBottom(PageBottomMarginMm, Unit.Millimetre);
                page.MarginHorizontal(0, Unit.Millimetre);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Helvetica"));

                page.Content().Column(col => {
                    var senderLine = JoinNonEmpty(", ",
                        envelope.SenderName,
                        envelope.SenderStreet,
                        JoinNonEmpty(" ", envelope.SenderPostcode, envelope.SenderCity),
                        envelope.SenderCountry);

                    col.Item()
                        .PaddingTop(SenderTopMm, Unit.Millimetre)
                        .PaddingLeft(AddressLeftMm, Unit.Millimetre)
                        .Text(senderLine)
                        .FontSize(8).FontColor(Colors.Grey.Darken2);

                    col.Item()
                        .PaddingTop(SenderToRecipientGapMm, Unit.Millimetre)
                        .PaddingLeft(AddressLeftMm, Unit.Millimetre)
                        .Column(rec => {
                            AddLine(rec, envelope.RecipientName);
                            AddLine(rec, envelope.RecipientStreet);
                            AddLine(rec, JoinNonEmpty(" ", envelope.RecipientPostcode, envelope.RecipientCity));
                            AddLine(rec, envelope.RecipientCountry);
                        });

                    col.Item()
                        .PaddingTop(BodyTopGapMm, Unit.Millimetre)
                        .PaddingHorizontal(BodyHorizontalMarginMm, Unit.Millimetre)
                        .Column(inner => {
                            inner.Spacing(8);
                            MarkdownPdfRenderer.RenderInto(inner, body);
                        });
                });

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

    private static void AddLine(ColumnDescriptor col, string text) {
        if (string.IsNullOrWhiteSpace(text))
            return;
        col.Item().Text(text);
    }

    private static string JoinNonEmpty(string separator, params string[] parts) {
        var nonEmpty = new System.Collections.Generic.List<string>();
        foreach (var part in parts) {
            if (!string.IsNullOrWhiteSpace(part))
                nonEmpty.Add(part);
        }
        return string.Join(separator, nonEmpty);
    }
}
