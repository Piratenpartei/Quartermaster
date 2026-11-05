using System;
using System.Security.Cryptography;
using System.Text;

namespace Quartermaster.Data.Templates;

/// <summary>Fingerprint of the texts a system template is seeded with, to tell rows nobody edited from edited ones.</summary>
public static class TemplateSeedHash {
    public const int Length = 64;

    private const char FieldSeparator = '\u001f';
    private const char NoSubject = '\u001e';

    public static string Compute(string displayName, string? subject, string body) {
        var text = $"{displayName}{FieldSeparator}{(subject == null ? NoSubject.ToString() : subject)}{FieldSeparator}{body}";
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    public static string Compute(Template template) {
        return Compute(template.DisplayName, template.Subject, template.Body);
    }
}
