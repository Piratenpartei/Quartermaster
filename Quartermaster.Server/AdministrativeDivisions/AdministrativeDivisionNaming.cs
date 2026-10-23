using System;
using System.Collections.Generic;
using System.Linq;
using Quartermaster.Data.AdministrativeDivisions;

namespace Quartermaster.Server.AdministrativeDivisions;

/// <summary>
/// Collects every name of a division from GeoNames and GV100AD and picks the one shown. GeoNames gives some divisions an English
/// or Sorbian main name ("Upper Franconia", "Wokrejs Sprjewja-Nysa"); its preferred German name, and for Regierungsbezirke the
/// GV100AD name, replaces it. The Länder keep the short name the loader picked ("Hamburg" rather than "Freie und Hansestadt Hamburg").
/// </summary>
public static class AdministrativeDivisionNaming {
    public const int StateDepth = 4;
    public const int RegionDepth = 5;
    public const int CountyDepth = 6;
    private const string German = "de";

    /// <summary>GeoNames calls some Gemeinden by their association ("Samtgemeinde Bersenbrück"), which a Gemeinde must not be shown as.</summary>
    private static readonly string[] AssociationPrefixes = ["Samtgemeinde ", "Verbandsgemeinde ", "Verwaltungsgemeinschaft ", "Amt "];

    public static List<ParsedDivisionName> Apply(AdministrativeDivisionParseResult parsed, IReadOnlyList<GeoNamesAlternateName> alternates, Gv100AdData? gv100Ad) {
        var alternatesById = alternates
            .GroupBy(a => a.GeoNameId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var officialNames = OfficialNames(gv100Ad);

        var names = new List<ParsedDivisionName>();
        foreach (var division in parsed.Divisions) {
            if (division.AdminCode == null || !parsed.OriginsByAdminCode.TryGetValue(division.AdminCode, out var origin))
                continue;

            var own = alternatesById.GetValueOrDefault(origin.GeoNameId) ?? [];
            var official = officialNames.GetValueOrDefault((division.Depth, division.AdminCode.TrimStart('0')));
            var shown = ShownName(division.Depth, own, official);
            if (shown != null)
                division.Name = shown;

            names.AddRange(NamesOf(division.AdminCode, origin, own, official));
        }

        return names;
    }

    private static string? ShownName(int depth, List<GeoNamesAlternateName> alternates, string? official) {
        if (depth == StateDepth)
            return null;
        if (depth == RegionDepth && official != null)
            return official;

        var preferred = alternates
            .Where(a => a.Language == German && a.IsPreferred && !a.IsHistoric && !a.IsColloquial)
            .Where(a => depth != AdministrativeDivisionRepository.MunicipalityDepth || !NamesAnAssociation(a.Name))
            .ToList();
        return (preferred.FirstOrDefault(a => !a.IsShort) ?? preferred.FirstOrDefault())?.Name;
    }

    private static bool NamesAnAssociation(string name) {
        return AssociationPrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal));
    }

    private static IEnumerable<ParsedDivisionName> NamesOf(string adminCode, AdministrativeDivisionOrigin origin, List<GeoNamesAlternateName> alternates, string? official) {
        var all = new List<ParsedDivisionName> {
            new(adminCode, origin.MainName, null, AdministrativeDivisionNameSource.GeoNames, false, false)
        };
        all.AddRange(alternates.Select(a => new ParsedDivisionName(adminCode, a.Name, a.Language.Length == 0 ? null : a.Language,
            AdministrativeDivisionNameSource.GeoNamesAlternate, a.IsPreferred, a.IsHistoric)));
        if (official != null)
            all.Add(new ParsedDivisionName(adminCode, official, German, AdministrativeDivisionNameSource.Gv100Ad, false, false));

        return all.DistinctBy(n => (n.Name, n.Language, n.Source));
    }

    /// <summary>GV100AD names by depth and admin code; GV100AD keys carry leading zeros our admin codes lack.</summary>
    private static Dictionary<(int Depth, string Code), string> OfficialNames(Gv100AdData? gv100Ad) {
        var names = new Dictionary<(int Depth, string Code), string>();
        if (gv100Ad == null)
            return names;

        foreach (var region in gv100Ad.Regions)
            names.TryAdd((RegionDepth, region.Key.TrimStart('0')), region.Name);
        foreach (var county in gv100Ad.Counties)
            names.TryAdd((CountyDepth, county.Key.TrimStart('0')), county.Name);
        foreach (var municipality in gv100Ad.Municipalities)
            names.TryAdd((AdministrativeDivisionRepository.MunicipalityDepth, municipality.Ags.TrimStart('0')), municipality.Name);

        return names;
    }
}
