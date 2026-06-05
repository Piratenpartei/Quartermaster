using System;
using System.Globalization;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Quartermaster.Api.Forms.ExpenseReimbursements;

namespace Quartermaster.Server.Forms.ExpenseReimbursements;

/// <summary>
/// Renders an Auslagenerstattung (expense reimbursement confirmation) form as a single-page A4 PDF
/// using QuestPDF. Visual layout mirrors the Pirate Party reference form
/// (https://wiki.piratenpartei.de/wiki/Auslagenerstattung); fields the user fills in are populated
/// from the supplied <see cref="ExpenseReimbursementInputDTO"/>, fields filled later by the
/// treasurer or chapter board stay blank.
/// </summary>
public static class ExpenseReimbursementPdfRenderer {
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    private const string AmountPlaceholder = "________";

    public static byte[] Render(ExpenseReimbursementInputDTO input, string? resolvedBic, string? logoSvg) {
        var document = Document.Create(container => {
            container.Page(page => {
                page.Margin(36);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Helvetica"));

                page.Content().Column(col => {
                    col.Spacing(8);
                    col.Item().Element(c => ComposeHeader(c, input, logoSvg));
                    col.Item().PaddingTop(4).Text("Hiermit bestätige ich");
                    col.Item().Element(c => ComposePersonalBlock(c, input));
                    col.Item().Element(c => ComposeChapterBlock(c, input));
                    col.Item().PaddingTop(6).LineHorizontal(1.5f).LineColor(Colors.Black);
                    col.Item().Text("Folgende Leistung(en) für die Piratenpartei Deutschland erbracht zu haben:");
                    col.Item().Element(c => ComposeLineItemTable(c, input));
                    col.Item().Element(c => ComposePayoutBlock(c, input, resolvedBic));
                    col.Item().PaddingTop(12).Element(c => ComposeFirstSignature(c, input));
                    col.Item().PaddingTop(6).Element(ComposeNotice);
                    if (input.PayoutAmount > 0m && input.PayoutMethod == ExpensePayoutMethodDTO.Cash) {
                        col.Item().PaddingTop(12).Element(ComposeSecondSignature);
                    }
                    col.Item().PaddingTop(12).Element(ComposeChapterFooterBoxes);
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, ExpenseReimbursementInputDTO input, string? logoSvg) {
        container.Row(row => {
            row.RelativeItem().Column(col => {
                col.Item().Text(t => {
                    t.Span("Bestätigung von Auslagen").FontSize(20).Bold();
                    t.Span(" (Version 1.02)").FontSize(8);
                });
                col.Item().PaddingTop(2).Text(string.IsNullOrWhiteSpace(input.ChapterName) ? "Name der Gliederung" : input.ChapterName)
                    .FontSize(13).FontColor(Colors.Grey.Darken2);
            });
            if (!string.IsNullOrEmpty(logoSvg)) {
                row.ConstantItem(140).Svg(logoSvg);
            }
        });
    }

    private static void ComposePersonalBlock(IContainer container, ExpenseReimbursementInputDTO input) {
        container.Column(col => {
            col.Spacing(2);

            col.Item().Row(row => {
                row.ConstantItem(60).Text("Name").FontColor(Colors.Grey.Darken2);
                row.RelativeItem().BorderBottom(0.5f).Text(FullName(input));
                row.ConstantItem(20).Text("");
                row.ConstantItem(80).Text("Mitglieds-Nr.").FontColor(Colors.Grey.Darken2);
                row.ConstantItem(80).BorderBottom(0.5f).Text(input.MemberNumber?.ToString(De) ?? "");
            });

            col.Item().Row(row => {
                row.ConstantItem(60).Text("Adresse").FontColor(Colors.Grey.Darken2);
                row.RelativeItem().BorderBottom(0.5f).Text(input.Street);
            });

            col.Item().Row(row => {
                row.ConstantItem(60).Text("").FontColor(Colors.Grey.Darken2);
                row.RelativeItem().BorderBottom(0.5f).Text($"{input.PostCode} {input.City}".Trim());
            });
        });
    }

    private static void ComposeChapterBlock(IContainer container, ExpenseReimbursementInputDTO input) {
        container.PaddingTop(8).Row(row => {
            row.ConstantItem(60).Text("Funktion").FontColor(Colors.Grey.Darken2);
            row.RelativeItem().BorderBottom(0.5f).Text(input.Function);
            row.ConstantItem(20).Text("");
            row.ConstantItem(80).Text("Beschluss vom").FontColor(Colors.Grey.Darken2);
            row.ConstantItem(80).BorderBottom(0.5f).Text(FormatDate(input.MotionDate));
        });
    }

    private static void ComposeLineItemTable(IContainer container, ExpenseReimbursementInputDTO input) {
        container.PaddingTop(2).Table(table => {
            table.ColumnsDefinition(c => {
                c.RelativeColumn(4);
                c.RelativeColumn(2);
                c.ConstantColumn(80);
            });

            table.Header(header => {
                header.Cell().Element(HeaderCell).Text("Erbrachte Leistung / Anschaffung").Bold();
                header.Cell().Element(HeaderCell).Text("Verbuchung*").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("Wert").Bold();
            });

            foreach (var item in input.LineItems) {
                table.Cell().Element(BodyCell).Text(item.Description);
                table.Cell().Element(BodyCell).Text("");
                table.Cell().Element(BodyCell).AlignRight().Text(FormatMoney(item.Value));
            }

            table.Footer(footer => {
                footer.Cell().ColumnSpan(2).Element(FooterCell).Text(t => {
                    t.Span("*wird vom Schatzmeister ausgefüllt.").FontSize(8).Italic();
                    t.Span("    ");
                    t.Span("Summe ∑").Bold();
                });
                footer.Cell().Element(FooterCell).AlignRight().Text(FormatMoney(input.LineItems.Sum(i => i.Value))).Bold();
            });

            static IContainer HeaderCell(IContainer c) =>
                c.Background(Colors.Grey.Lighten3).Border(0.5f).PaddingVertical(4).PaddingHorizontal(6).AlignMiddle();
            static IContainer BodyCell(IContainer c) =>
                c.Border(0.5f).PaddingVertical(2).PaddingHorizontal(6).MinHeight(14).AlignMiddle();
            static IContainer FooterCell(IContainer c) =>
                c.Border(0.5f).PaddingVertical(4).PaddingHorizontal(6).AlignMiddle();
        });
    }

    private static void ComposePayoutBlock(IContainer container, ExpenseReimbursementInputDTO input, string? resolvedBic) {
        var donatePortion = input.DonatedAmount > 0m;
        var payoutPortion = input.PayoutAmount > 0m;

        container.PaddingTop(6).Column(col => {
            col.Spacing(2);

            col.Item().Element(c => CheckboxWithAmount(c, donatePortion,
                "Ich verzichte auf Auszahlung von",
                donatePortion ? FormatAmount(input.DonatedAmount) : AmountPlaceholder,
                "Euro und spende diesen Betrag stattdessen."));

            col.Item().Element(c => Checkbox(c, donatePortion && input.RequestsDonationReceipt,
                "Ich bitte um die Ausstellung einer Zuwendungsbescheinigung."));

            col.Item().Element(c => CheckboxWithAmount(c, payoutPortion,
                "Ich bitte um Auszahlung von",
                payoutPortion ? FormatAmount(input.PayoutAmount) : AmountPlaceholder,
                "Euro."));

            col.Item().Element(c => Checkbox(c, payoutPortion && input.PayoutMethod == ExpensePayoutMethodDTO.BankTransfer,
                "Ich bitte um die Auszahlung des Betrags per Banküberweisung auf folgendes Konto"));
            col.Item().PaddingLeft(20).Row(row => {
                row.ConstantItem(40).Text("IBAN").FontColor(Colors.Grey.Darken2);
                row.RelativeItem().BorderBottom(0.5f).Text(input.Iban ?? "");
            });
            col.Item().PaddingLeft(20).Row(row => {
                row.ConstantItem(40).Text("BIC").FontColor(Colors.Grey.Darken2);
                row.RelativeItem().BorderBottom(0.5f).Text(resolvedBic ?? "");
            });
            col.Item().Element(c => Checkbox(c, payoutPortion && input.PayoutMethod == ExpensePayoutMethodDTO.Cash,
                "Ich bitte um Barauszahlung."));
        });
    }

    private static void Checkbox(IContainer container, bool ticked, string label) {
        container.Row(row => {
            row.ConstantItem(16).AlignTop().Border(0.5f).Height(11).AlignCenter().AlignMiddle()
                .Text(ticked ? "X" : " ").FontSize(9).Bold();
            row.RelativeItem().PaddingLeft(4).Text(label);
        });
    }

    private static void CheckboxWithAmount(IContainer container, bool ticked, string textBefore, string amount, string textAfter) {
        container.Row(row => {
            row.ConstantItem(16).AlignTop().Border(0.5f).Height(11).AlignCenter().AlignMiddle()
                .Text(ticked ? "X" : " ").FontSize(9).Bold();
            row.RelativeItem().PaddingLeft(4).Text(t => {
                t.Span(textBefore);
                t.Span(" ");
                t.Span(amount).Bold();
                t.Span(" ");
                t.Span(textAfter);
            });
        });
    }

    private static void ComposeFirstSignature(IContainer container, ExpenseReimbursementInputDTO input) {
        container.Row(row => {
            row.RelativeItem().Column(col => {
                col.Item().Text($"{input.SignatureLocation}{(string.IsNullOrEmpty(input.SignatureLocation) ? "" : ", ")}{FormatDate(input.FormDate)}");
                col.Item().BorderTop(0.5f).PaddingTop(2).Text("Ort, Datum").FontSize(8).FontColor(Colors.Grey.Darken2);
            });
            row.ConstantItem(20).Text("");
            row.RelativeItem().Column(col => {
                col.Item().Text(" ");
                col.Item().BorderTop(0.5f).PaddingTop(2).Text("Unterschrift des Mitglieds").FontSize(8).FontColor(Colors.Grey.Darken2);
            });
        });
    }

    private static void ComposeNotice(IContainer container) {
        container.Text(t => {
            t.DefaultTextStyle(x => x.FontSize(9));
            t.Span("Hinweis: Es können nur Auslagen eingereicht werden, welche ");
            t.Span("vorher").Bold();
            t.Span(" auf Beschluss des Vorstands genehmigt wurden. Belege müssen immer im Original zusammen mit diesem Formular an den Schatzmeister übergegeben werden. ");
            t.Span("Ohne Originalbelege kann keine Erstattung erfolgen.").Bold();
        });
    }

    private static void ComposeSecondSignature(IContainer container) {
        container.Column(col => {
            col.Item().Text("Bei Barauszahlung: Betrag erhalten am ").FontSize(9);
            col.Item().PaddingTop(8).Row(row => {
                row.RelativeItem().Column(c => {
                    c.Item().Text("");
                    c.Item().BorderTop(0.5f).PaddingTop(2).Text("Ort, Datum").FontSize(8).FontColor(Colors.Grey.Darken2);
                });
                row.ConstantItem(20).Text("");
                row.RelativeItem().Column(c => {
                    c.Item().Text("");
                    c.Item().BorderTop(0.5f).PaddingTop(2).Text("Unterschrift des Mitglieds").FontSize(8).FontColor(Colors.Grey.Darken2);
                });
            });
        });
    }

    private static void ComposeChapterFooterBoxes(IContainer container) {
        container.Row(row => {
            foreach (var label in new[] { "Geschäftsstelle:", "Bankverbindung:", "Vorstand:" }) {
                row.RelativeItem().Border(0.5f).Padding(6).MinHeight(70).Column(col => {
                    col.Item().Text(label).FontSize(9).FontColor(Colors.Grey.Darken1);
                });
                row.ConstantItem(8).Text("");
            }
        });
    }

    private static string FullName(ExpenseReimbursementInputDTO input)
        => $"{input.FirstName} {input.LastName}".Trim();

    private static string FormatDate(DateTime value)
        => value == default ? "" : value.ToString("dd.MM.yyyy", De);

    private static string FormatMoney(decimal value)
        => value.ToString("N2", De) + " €";

    private static string FormatAmount(decimal value)
        => value.ToString("N2", De);
}
