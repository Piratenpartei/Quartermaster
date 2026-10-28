using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Quartermaster.Api.ApplicantRegister;
using Quartermaster.Data.ApplicantRegister;

namespace Quartermaster.Server.ApplicantRegister;

/// <summary>German texts about register entries for the federal ticket and the hit notification.</summary>
public static class RegisterNoticeText {
    public const string Header = "Zustimmung des Bundesvorstands erforderlich (§ 3 Abs. 2 BS):";
    public const string SelfDeclarationLine = "nach eigener Angabe: frühere Ablehnung";

    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    private static readonly (RegisterMatchFields Field, string Label)[] FieldLabels = [
        (RegisterMatchFields.FirstName, "Vorname"),
        (RegisterMatchFields.LastName, "Nachname"),
        (RegisterMatchFields.DateOfBirth, "Geburtsdatum"),
        (RegisterMatchFields.Street, "Straße"),
        (RegisterMatchFields.PostCodeCity, "Postleitzahl und Ort"),
        (RegisterMatchFields.Email, "E-Mail")
    ];

    /// <summary>Returns null when there is neither a hit nor a self-declaration.</summary>
    public static string? Build(IReadOnlyList<ApplicationRegisterHit> hits, IReadOnlyList<ApplicantRegisterEntry> entries, bool selfDeclared) {
        if (hits.Count == 0 && !selfDeclared)
            return null;

        var lines = new List<string> { Header };
        lines.AddRange(HitLines(hits, entries).Select(line => "- " + line));
        if (selfDeclared)
            lines.Add("- " + SelfDeclarationLine);
        return string.Join("\n", lines);
    }

    /// <summary>One line per hit whose entry still exists.</summary>
    public static List<string> HitLines(IReadOnlyList<ApplicationRegisterHit> hits, IReadOnlyList<ApplicantRegisterEntry> entries) {
        var lines = new List<string>();
        foreach (var hit in hits) {
            var entry = entries.FirstOrDefault(e => e.Id == hit.EntryId);
            if (entry != null)
                lines.Add(EntryLine(entry, hit.MatchedFields));
        }
        return lines;
    }

    private static string EntryLine(ApplicantRegisterEntry entry, RegisterMatchFields fields) {
        var matched = FieldLabels.Where(f => fields.HasFlag(f.Field)).Select(f => f.Label);
        return $"{KindLabel(entry.Kind)} am {entry.OccurredOn.ToString("dd.MM.yyyy", German)}: {entry.FirstName} {entry.LastName} (übereinstimmend: {string.Join(", ", matched)})";
    }

    public static string KindLabel(ApplicantRegisterKind kind) {
        return kind switch {
            ApplicantRegisterKind.Rejected => "Aufnahme abgelehnt",
            ApplicantRegisterKind.Expelled => "Ausgeschlossen",
            ApplicantRegisterKind.LeftDuringExpulsion => "Während eines Ausschlussverfahrens ausgetreten",
            _ => kind.ToString()
        };
    }
}
