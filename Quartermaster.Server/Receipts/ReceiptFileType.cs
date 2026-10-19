using System;

namespace Quartermaster.Server.Receipts;

/// <summary>The accepted receipt formats, recognised by their first bytes rather than the file name.</summary>
public record ReceiptFileType(string ContentType, string Extension) {
    public const int HeaderLength = 8;

    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];

    public static readonly ReceiptFileType Pdf = new("application/pdf", ".pdf");
    public static readonly ReceiptFileType Png = new("image/png", ".png");
    public static readonly ReceiptFileType Jpeg = new("image/jpeg", ".jpg");

    public static ReceiptFileType? Detect(ReadOnlySpan<byte> header) {
        if (header.StartsWith(PdfSignature))
            return Pdf;
        if (header.StartsWith(PngSignature))
            return Png;

        return header.StartsWith(JpegSignature) ? Jpeg : null;
    }

    public bool IsImage => ContentType != Pdf.ContentType;
}
