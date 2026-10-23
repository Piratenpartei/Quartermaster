using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Quartermaster.Server.AdministrativeDivisions;

/// <summary>Splits division names and search terms into comparable words: lowercase, umlauts and accents dropped, punctuation ignored.</summary>
public static class DivisionNameTokenizer {
    private static readonly HashSet<string> DesignatorWords = [
        "landkreis", "kreis", "kreisfreie", "kreisstadt", "stadt", "stadtkreis", "regierungsbezirk", "stadtbezirk", "bezirk", "region", "regionalverband",
        "samtgemeinde", "verbandsgemeinde", "verwaltungsgemeinschaft", "gemeinde", "amt", "markt", "flecken", "landeshauptstadt",
        "hansestadt", "hanse", "universitatsstadt", "wissenschaftsstadt", "freie", "und", "district", "municipality"
    ];

    public static bool IsDesignator(string word) {
        return DesignatorWords.Contains(word);
    }

    public static TokenizedName Tokenize(string name) {
        var words = Words(name);
        var core = words.Where(w => !IsDesignator(w)).ToArray();
        var designators = words.Where(IsDesignator).ToArray();

        // A name made only of designator words ("Stadt") would otherwise match nothing.
        if (core.Length == 0) {
            core = designators;
            designators = [];
        }

        return new TokenizedName(core, designators, string.Concat(core));
    }

    public static List<string> Words(string text) {
        return Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    public static string Normalize(string text) {
        // "München", "Muenchen" and "Munchen" all become "munchen"; names like "Soest" change too, but the same way on both sides.
        var written = text.ToLowerInvariant()
            .Replace("ae", "a")
            .Replace("oe", "o")
            .Replace("ue", "u")
            .Replace("ß", "ss");

        var builder = new StringBuilder(written.Length);
        foreach (var c in written.Normalize(NormalizationForm.FormD)) {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;

            builder.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }

        return builder.ToString();
    }
}
