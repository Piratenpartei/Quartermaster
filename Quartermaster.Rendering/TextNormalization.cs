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
