using System.Collections.Generic;
using System.IO;

namespace Quartermaster.Data.AdministrativeDivisions;

/// <summary>
/// Reads the fixed-width Destatis GV100AD file (format "Datensatzbeschreibung GV100AD", records of type 50 and 60).
/// The format counts positions in bytes, but the file is UTF-8 and the positions only hold for characters: a name
/// with an umlaut would shift every later byte column.
/// </summary>
public static class Gv100AdParser {
    private const string AssociationRecord = "50";
    private const string MunicipalityRecord = "60";
    private const int KindColumnEnd = 124;

    /// <summary>Associations whose administration also certifies for its member municipalities (Amt, Samtgemeinde, Verbandsgemeinde and the like); 50 is a municipality without association.</summary>
    public static bool CertifiesForMembers(int kind) {
        return kind >= 51 && kind <= 58;
    }

    public static Gv100AdData Parse(TextReader reader) {
        var associations = new List<Gv100AdAssociation>();
        var municipalities = new List<Gv100AdMunicipality>();

        string? line;
        while ((line = reader.ReadLine()) != null) {
            if (line.Length < KindColumnEnd)
                continue;

            var recordType = line[..2];
            if (recordType == AssociationRecord) {
                associations.Add(new Gv100AdAssociation(Region(line) + Association(line), Name(line), Kind(line)));
            } else if (recordType == MunicipalityRecord) {
                municipalities.Add(new Gv100AdMunicipality(Region(line) + line[15..18], Region(line) + Association(line), Name(line)));
            }
        }

        return new Gv100AdData(associations, municipalities);
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
