using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Quartermaster.Server.Tests.Texts;

/// <summary>
/// Finds person nouns that break the rule in <c>Translations.md</c>: German texts use the generic masculine, without gender signs,
/// inner capital I or participles such as "Antragstellende".
/// </summary>
public static class GermanPersonNounScanner {
    private static readonly Regex _genderSign = new(@"\b\p{L}+:(?:in|innen|r|e|n)\b|\b\p{Lu}\p{Ll}+(?:\*|_|/-?)(?:in|innen)\b");
    private static readonly Regex _innerCapital = new(@"\b\p{Lu}\p{Ll}+Innen\b");
    private static readonly Regex _participleNoun = new(@"\b\p{Lu}\p{Ll}*(?:ende|enden|ender)\b");
    private static readonly Regex _participlePerson = new(@"\b\p{Ll}+(?:ende|enden|ender) Person(?:en)?\b");

    /// <summary>Capitalised words ending like a participle that are no person nouns, or person nouns that are the generic masculine.</summary>
    private static readonly HashSet<string> _allowedWords = [
        "Ende", "Reiseende", "Spende", "Spenden", "Sende", "Senden", "Absenden", "Absender", "Beenden", "Anwenden", "Aufwenden", "Verwende",
        "Render", "Laender",
        "Abweichende", "Ausgehende", "Bestehende", "Eingehende", "Folgende", "Geltende", "Kommende", "Laufende", "Schwebende",
        "Konkurrierende", "Konkurrierenden", "Konkurrierender",
        "Vorsitzende", "Vorsitzenden", "Vorsitzender", "Stellvertretende", "Stellvertretenden", "Stellvertretender"
    ];

    /// <summary>Quotes of official wording that must stay as printed.</summary>
    private static readonly string[] _allowedQuotes = [
        "Nicht von der unterzeichnenden Person auszufüllen"
    ];

    public static List<string> FindViolations(string line) {
        foreach (var quote in _allowedQuotes)
            line = line.Replace(quote, "");

        var found = new List<string>();
        found.AddRange(_genderSign.Matches(line).Select(m => m.Value));
        found.AddRange(_innerCapital.Matches(line).Select(m => m.Value));
        found.AddRange(_participleNoun.Matches(line).Select(m => m.Value).Where(w => !_allowedWords.Contains(w)));
        found.AddRange(_participlePerson.Matches(line).Select(m => m.Value));
        return found;
    }
}
