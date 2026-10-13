using System.Text;

namespace Quartermaster.Rendering;

public static class TextNormalization {
    /// <summary>
    /// Returns the text in Unicode normalization form C. Lone surrogates, which make Normalize throw, are replaced by U+FFFD first. Null yields an empty string.
    /// </summary>
    public static string ToNfc(string? text) {
        if (string.IsNullOrEmpty(text))
            return "";

        return ReplaceLoneSurrogates(text).Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Removes control characters (including CR, LF and tab) and the Unicode line and paragraph separators, so the result is safe for a single-line field. Null yields an empty string.
    /// </summary>
    public static string RemoveControlCharacters(string? text) {
        if (string.IsNullOrEmpty(text))
            return "";

        var sb = new StringBuilder(text.Length);
        foreach (var c in text) {
            if (IsLineOrControlCharacter(c))
                continue;
            sb.Append(c);
        }

        return sb.ToString();
    }

    private static bool IsLineOrControlCharacter(char c) {
        if (char.IsControl(c))
            return true;

        return c == '\u2028' || c == '\u2029';
    }

    private static string ReplaceLoneSurrogates(string text) {
        StringBuilder? sb = null;
        for (var i = 0; i < text.Length; i++) {
            var c = text[i];
            if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])) {
                sb?.Append(c).Append(text[i + 1]);
                i++;
                continue;
            }

            if (!char.IsSurrogate(c)) {
                sb?.Append(c);
                continue;
            }

            sb ??= new StringBuilder(text.Length).Append(text, 0, i);
            sb.Append('�');
        }

        return sb == null ? text : sb.ToString();
    }
}
