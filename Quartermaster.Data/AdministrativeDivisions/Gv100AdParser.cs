using System;
using System.Collections.Generic;
using System.IO;

namespace Quartermaster.Data.AdministrativeDivisions;

/// <summary>
/// Reads the fixed-width Destatis GV100AD file (format "Datensatzbeschreibung GV100AD", records of type 20, 40, 50 and 60).
/// The format counts positions in bytes, but the file is UTF-8 and the positions only hold for characters: a name
/// with an umlaut would shift every later byte column.
/// </summary>
public static class Gv100AdParser {
    private const string RegionRecord = "20";
    private const string AssociationRecord = "50";
    private const string RegionPrefix = "Regierungsbezirk ";
    private const string RegionAbbreviation = "Reg.-Bez. ";
    private const string CountyRecord = "40";
    private const string MunicipalityRecord = "60";
    private const int KindColumnEnd = 124;

    /// <summary>Associations whose administration also certifies for its member municipalities (Amt, Samtgemeinde, Verbandsgemeinde and the like); 50 is a municipality without association.</summary>
    public static bool CertifiesForMembers(int kind) {
        return kind >= 51 && kind <= 58;
    }

    public static Gv100AdData Parse(TextReader reader) {
        var associations = new List<Gv100AdAssociation>();
        var municipalities = new List<Gv100AdMunicipality>();
        var regions = new List<Gv100AdRegion>();
        var counties = new List<Gv100AdCounty>();

        string? line;
        while ((line = reader.ReadLine()) != null) {
            if (line.Length < KindColumnEnd)
                continue;

            var recordType = line[..2];
            if (recordType == RegionRecord) {
                var region = RegionName(Name(line));
                if (region != null)
                    regions.Add(new Gv100AdRegion(line[10..13], region));
            } else if (recordType == CountyRecord) {
                counties.Add(new Gv100AdCounty(Region(line), Name(line)));
            } else if (recordType == AssociationRecord) {
                associations.Add(new Gv100AdAssociation(Region(line) + Association(line), Name(line), Kind(line)));
            } else if (recordType == MunicipalityRecord) {
                municipalities.Add(new Gv100AdMunicipality(Region(line) + line[15..18], Region(line) + Association(line), Name(line)));
            }
        }

        return new Gv100AdData(associations, municipalities, regions, counties);
    }

    /// <summary>
    /// "Regierungsbezirk Oberfranken" for "Oberfranken" or "Reg.-Bez. Düsseldorf"; null for the former Regierungsbezirke and the
    /// statistical regions of Lower Saxony, which GeoNames does not list.
    /// </summary>
    public static string? RegionName(string name) {
        if (name.StartsWith("früher:", StringComparison.Ordinal) || name.StartsWith("Statistische Region", StringComparison.Ordinal))
            return null;

        var bare = name.StartsWith(RegionAbbreviation, StringComparison.Ordinal) ? name[RegionAbbreviation.Length..] : name;
        return RegionPrefix + bare;
    }

    private static string Region(string line) {
        return line[10..15];
    }

    private static string Association(string line) {
        return line[18..22];
    }

    private static string Name(string line) {
        return line[22..72].Trim();
    }

    private static int Kind(string line) {
        return int.TryParse(line[122..124], out var kind) ? kind : 0;
    }
}
