using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Quartermaster.Data.AdministrativeDivisions;

/// <summary>
/// Reads a GeoNames alternate names file (alternatenames/DE.zip, tab separated). Keeps the names in Latin script and drops the rows
/// that are links, codes or transcriptions rather than names.
/// </summary>
public static class GeoNamesAlternateNameParser {
    private const int ColumnCount = 8;

    private static readonly HashSet<string> NonLanguageCodes = [
        "link", "wkdt", "post", "iata", "icao", "faac", "unlc", "lauc", "abbr", "phon", "piny", "tcid", "fr_1793"
    ];

    public static List<GeoNamesAlternateName> Parse(TextReader reader, IReadOnlySet<int> geoNameIds) {
        var names = new List<GeoNamesAlternateName>();
        string? line;
        while ((line = reader.ReadLine()) != null) {
            var split = line.Split('\t');
            if (split.Length < ColumnCount)
                continue;
            if (!int.TryParse(split[1], out var geoNameId) || !geoNameIds.Contains(geoNameId))
                continue;

            var name = split[3].Trim();
            if (!IsUsableName(split[2], name))
                continue;

            names.Add(new GeoNamesAlternateName(geoNameId, split[2], name, split[4] == "1", split[5] == "1", split[6] == "1", split[7] == "1"));
        }

        return names;
    }

    private static bool IsUsableName(string language, string name) {
        if (NonLanguageCodes.Contains(language))
            return false;
        if (name.Length == 0 || name.Length > AdministrativeDivisionName.NameMaxLength)
            return false;

        return name.All(IsLatin);
    }

    private static bool IsLatin(char c) {
        if (!char.IsLetter(c))
            return true;

        return c < 'ɐ' || (c >= 'Ḁ' && c <= 'ỿ');
    }
}
