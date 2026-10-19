using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using Quartermaster.Api.I18n;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Quartermaster.Data.Receipts;
using Quartermaster.Server.Receipts;

namespace Quartermaster.Server.TravelExpenses;

/// <summary>
/// Appends receipts to a generated form: an image becomes one A4 page with its label, a PDF is merged with all its
/// pages. A receipt that can't be merged (damaged or encrypted) gets a page saying so instead of failing the whole form.
/// </summary>
public class ReceiptAppender {
    private readonly ReceiptStorage _storage;
    private readonly I18nService _i18n;
    private readonly ILogger<ReceiptAppender> _logger;

    public ReceiptAppender(ReceiptStorage storage, I18nService i18n, ILogger<ReceiptAppender> logger) {
        _storage = storage;
        _i18n = i18n;
        _logger = logger;
    }

    public byte[] Append(byte[] formPdf, IReadOnlyList<UserReceipt> receipts) {
        if (receipts.Count == 0)
            return formPdf;

        var workDir = Directory.CreateTempSubdirectory("qm-receipts-");
        try {
            var basePath = Path.Combine(workDir.FullName, "form.pdf");
            File.WriteAllBytes(basePath, formPdf);
            var parts = new List<string>();
            for (var i = 0; i < receipts.Count; i++)
                parts.Add(PartFor(receipts[i], i + 1, workDir.FullName));

            var outputPath = Path.Combine(workDir.FullName, "merged.pdf");
            var operation = DocumentOperation.LoadFile(basePath);
            foreach (var part in parts)
                operation = operation.MergeFile(part);
            operation.Save(outputPath);
            return File.ReadAllBytes(outputPath);
        } finally {
            workDir.Delete(recursive: true);
        }
    }

    // A PDF receipt is merged as it is once a test merge succeeded; images and broken files become a rendered page.
    private string PartFor(UserReceipt receipt, int number, string workDir) {
        var path = _storage.PathOf(receipt.OwnerUserId, receipt.StoredName);
        var label = _i18n[I18nParams.With(I18nKey.Ui.TravelExpense.Pdf.AttachmentLabel, ("number", number.ToString()), ("label", receipt.Label))];
        var partPath = Path.Combine(workDir, $"receipt-{number}.pdf");
        if (!File.Exists(path)) {
            File.WriteAllBytes(partPath, NoticePage(label, _i18n[I18nKey.Ui.TravelExpense.Pdf.ReceiptMissing]));
            return partPath;
        }
        if (receipt.ContentType != ReceiptFileType.Pdf.ContentType) {
            File.WriteAllBytes(partPath, ImagePage(label, File.ReadAllBytes(path)));
            return partPath;
        }

        try {
            DocumentOperation.LoadFile(path).Save(partPath);
            return partPath;
        } catch (Exception ex) {
            _logger.LogWarning(ex, "Receipt {ReceiptId} could not be merged", receipt.Id);
            File.WriteAllBytes(partPath, NoticePage(label, _i18n[I18nKey.Ui.TravelExpense.Pdf.ReceiptNotMergeable]));
            return partPath;
        }
    }

    private static byte[] ImagePage(string label, byte[] image) {
        return Document.Create(container => container.Page(page => {
            page.Size(PageSizes.A4);
            page.Margin(28);
            page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Helvetica"));
            page.Header().PaddingBottom(6).Text(label).Bold();
            page.Content().Image(image).FitArea();
        })).GeneratePdf();
    }

    private static byte[] NoticePage(string label, string message) {
        return Document.Create(container => container.Page(page => {
            page.Size(PageSizes.A4);
            page.Margin(28);
            page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Helvetica"));
            page.Header().PaddingBottom(6).Text(label).Bold();
            page.Content().Text(message);
        })).GeneratePdf();
    }
}
