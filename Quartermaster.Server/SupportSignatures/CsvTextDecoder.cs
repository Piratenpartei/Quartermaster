using System;
using System.Text;
using System.Text.Unicode;

namespace Quartermaster.Server.SupportSignatures;

/// <summary>Reads an uploaded or bundled CSV as UTF-8, falling back to Windows-1252, which Excel uses for "CSV (Trennzeichen-getrennt)".</summary>
public static class CsvTextDecoder {
    private const int WindowsLatinCodePage = 1252;

    public static string Decode(byte[] bytes) {
        var preamble = Encoding.UTF8.Preamble;
        var start = bytes.AsSpan().StartsWith(preamble) ? preamble.Length : 0;
        var content = bytes.AsSpan(start);
        if (Utf8.IsValid(content))
            return Encoding.UTF8.GetString(content);

        return CodePagesEncodingProvider.Instance.GetEncoding(WindowsLatinCodePage)!.GetString(content);
    }
}
