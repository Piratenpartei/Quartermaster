using CsvHelper.Configuration;

namespace Quartermaster.Server.Members;

public class MemberCsvRecord {
    public int USER_Mitgliedsnummer { get; set; }
    public string? USER_refAufnahme { get; set; }
    public string Name1 { get; set; } = "";
    public string Name2 { get; set; } = "";
    public string? LieferStrasse { get; set; }
    public string? LieferLand { get; set; }
    public string? LieferPLZ { get; set; }
    public string? LieferOrt { get; set; }
    public string? Telefon { get; set; }
    public string? EMail { get; set; }
    public string? USER_LV { get; set; }
    public string? USER_Bezirk { get; set; }
    public string? USER_Kreis { get; set; }
    public string? USER_Beitrag { get; set; }
    public string? USER_redBeitrag { get; set; }
    public string? USER_Umfragen { get; set; }
    public string? USER_Aktionen { get; set; }
    public string? USER_Newsletter { get; set; }
    public string? USER_Geburtsdatum { get; set; }
    public string? USER_Postbounce { get; set; }
    public string? USER_Bundesland { get; set; }
    public string? USER_Eintrittsdatum { get; set; }
    public string? USER_Austrittsdatum { get; set; }
    public string? USER_Erstbeitrag { get; set; }
    public string? USER_Landkreis { get; set; }
    public string? USER_Gemeinde { get; set; }
    public string? USER_Staatsbuergerschaft { get; set; }
    public string? USER_zStimmberechtigung { get; set; }
    public string? USER_zoffenerbeitragtotal { get; set; }
    public string? USER_redBeitragEnde { get; set; }
    public string? USER_Schwebend { get; set; }
}

public sealed class MemberCsvRecordMap : ClassMap<MemberCsvRecord> {
    public MemberCsvRecordMap() {
        Map(m => m.USER_Mitgliedsnummer).Name("USER_Mitgliedsnummer");
        Map(m => m.USER_refAufnahme).Name("USER_refAufnahme");
        Map(m => m.Name1).Name("Name1");
        Map(m => m.Name2).Name("Name2");
        Map(m => m.LieferStrasse).Name("LieferStrasse");
        Map(m => m.LieferLand).Name("LieferLand");
        Map(m => m.LieferPLZ).Name("LieferPLZ");
        Map(m => m.LieferOrt).Name("LieferOrt");
        Map(m => m.Telefon).Name("Telefon");
        Map(m => m.EMail).Name("EMail");
        Map(m => m.USER_LV).Name("USER_LV");
        Map(m => m.USER_Bezirk).Name("USER_Bezirk");
        Map(m => m.USER_Kreis).Name("USER_Kreis");
        Map(m => m.USER_Beitrag).Name("USER_Beitrag");
        Map(m => m.USER_redBeitrag).Name("USER_redBeitrag");
        Map(m => m.USER_Umfragen).Name("USER_Umfragen");
        Map(m => m.USER_Aktionen).Name("USER_Aktionen");
        Map(m => m.USER_Newsletter).Name("USER_Newsletter");
        Map(m => m.USER_Geburtsdatum).Name("USER_Geburtsdatum");
        Map(m => m.USER_Postbounce).Name("USER_Postbounce");
        Map(m => m.USER_Bundesland).Name("USER_Bundesland");
        Map(m => m.USER_Eintrittsdatum).Name("USER_Eintrittsdatum");
        Map(m => m.USER_Austrittsdatum).Name("USER_Austrittsdatum");
        Map(m => m.USER_Erstbeitrag).Name("USER_Erstbeitrag");
        Map(m => m.USER_Landkreis).Name("USER_Landkreis");
        Map(m => m.USER_Gemeinde).Name("USER_Gemeinde");
        Map(m => m.USER_Staatsbuergerschaft).Name("USER_Staatsbuergerschaft");
        Map(m => m.USER_zStimmberechtigung).Name("USER_zStimmberechtigung");
        Map(m => m.USER_zoffenerbeitragtotal).Name("USER_zoffenerbeitragtotal");
        Map(m => m.USER_redBeitragEnde).Name("USER_redBeitragEnde");
        Map(m => m.USER_Schwebend).Name("USER_Schwebend");
    }
}
