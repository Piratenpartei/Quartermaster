using System;
using System.Security.Cryptography;
using System.Text;
using Quartermaster.Rendering;

namespace Quartermaster.Server.AssemblyMotions.Wiki;

public static class WikiPageText {
    /// <summary>
    /// Brings page text into the form MediaWiki stores: NFC, LF line endings, no trailing whitespace. Applied to rendered text and to text read back from the wiki before hashing, so both sides compare equal.
    /// </summary>
    public static string Normalize(string? text) {
        var unified = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
        return TextNormalization.ToNfc(unified).TrimEnd();
    }

    /// <summary>Lowercase hex SHA-256 of the UTF-8 bytes of the normalised text.</summary>
    public static string Hash(string? text)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(text))));
}
