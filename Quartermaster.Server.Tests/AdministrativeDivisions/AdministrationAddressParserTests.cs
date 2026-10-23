using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Quartermaster.Server.AdministrativeDivisions;
using Quartermaster.Server.SupportSignatures;

namespace Quartermaster.Server.Tests.AdministrativeDivisions;

public class AdministrationAddressParserTests {
    private const string Header =
        "Land,,Satzart,Textkennzeichen,,Amtlicher Regionalschlüssel (ARS),Amtlicher Gemeindeschlüssel (AGS),Gemeinde/Stadt,Verwaltungssitz,Straße,PLZ,Ort,E-Mail-Adressen ,Fläche 1\n";

    private const string Sheet =
        "Zurück zum Inhalt,,,,,,,,,,,,,\n" +
        "\"Anschriften der Gemeinde- und Stadtverwaltungen in Deutschland am 31.01.2026\",,,,,,,,,,,,,\n" +
        Header +
        "03,Niedersachsen,50,52,Samtgemeinde,033575401,,Samtgemeinde Bothel,Samtgemeinde Bothel,Horstweg 17,27386,Bothel,samtgemeinde@bothel.de,1\n" +
        "03,Niedersachsen,60,64,Kreisangehörige Gemeinde,033575401006,03357006,Bothel,Samtgemeinde Bothel,Horstweg 17,27386,Bothel,samtgemeinde@bothel.de,1\n" +
        "03,Niedersachsen,60,64,Kreisangehörige Gemeinde,033575401009,03357009,Brockel,Samtgemeinde Bothel,Horstweg 17,27386,Bothel,samtgemeinde@bothel.de,1\n" +
        "03,Niedersachsen,60,61,Kreisfreie Stadt,031010000000,03101000,\"Braunschweig, Stadt\",Kreisfreie Stadt Braunschweig,Platz der Deutschen Einheit 1,38100,Braunschweig,stadt@braunschweig.de,1\n" +
        ",,,,,,,,,,,,,\n" +
        "Quelle: Gemeindeverzeichnis-Informationssystem GV-ISys,,,,,,,,,,,,,\n";

    [Test]
    public async Task Groups_municipality_rows_by_their_administration() {
        var data = AdministrationAddressParser.Parse(new StringReader(Sheet));

        var bothel = data.Offices.Single(o => o.Name == "Samtgemeinde Bothel");
        await Assert.That(data.Offices.Count).IsEqualTo(2);
        await Assert.That(data.Municipalities).IsEqualTo(3);
        await Assert.That(bothel.Key).IsEqualTo("03357|Samtgemeinde Bothel");
        await Assert.That(bothel.MunicipalityKeys.ToList()).IsEquivalentTo(["03357006", "03357009"]);
        await Assert.That(bothel.Street).IsEqualTo("Horstweg 17");
        await Assert.That(bothel.PostCode).IsEqualTo("27386");
        await Assert.That(bothel.City).IsEqualTo("Bothel");
        await Assert.That(bothel.Email).IsEqualTo("samtgemeinde@bothel.de");
    }

    [Test]
    public async Task Reads_a_semicolon_export_without_leading_zeros() {
        var sheet = Header.Replace(',', ';') +
            "3;Niedersachsen;60;64;Kreisangehörige Gemeinde;33575401006;3357006;Bothel;Samtgemeinde Bothel;Horstweg 17;27386;Bothel;;1\n";

        var data = AdministrationAddressParser.Parse(new StringReader(sheet));

        await Assert.That(data.Offices.Single().MunicipalityKeys.Single()).IsEqualTo("03357006");
        await Assert.That(data.Offices.Single().Email).IsNull();
    }

    [Test]
    public async Task Skips_rows_without_address() {
        var sheet = Header + "09,Bayern,60,61,Kreisfreie Stadt,091610000000,09161000,Ingolstadt,Ingolstadt,,,,,1\n";

        var data = AdministrationAddressParser.Parse(new StringReader(sheet));

        await Assert.That(data.WithoutAddress).IsEqualTo(1);
        await Assert.That(data.Offices).IsEmpty();
    }

    [Test]
    public async Task Verwaltungsgemeinschaft_certifies_for_its_members_outside_baden_wuerttemberg() {
        var sheet = Header +
            "09,Bayern,50,54,Verwaltungsgemeinschaft,091735101,,Verwaltungsgemeinschaft Benediktbeuern,Verwaltungsgemeinschaft Benediktbeuern,Prälatenstraße 3,83671,Benediktbeuern,,1\n" +
            "09,Bayern,60,64,Kreisangehörige Gemeinde,091735101114,09173114,Bichl,Gemeinde Bichl,Kocheler Straße 2,83673,Bichl,,1\n" +
            "09,Bayern,60,64,Kreisangehörige Gemeinde,091735101114,09173114,Bichl,Verwaltungsgemeinschaft Benediktbeuern,Prälatenstraße 3,83671,Benediktbeuern,,1\n" +
            "08,Baden-Württemberg,50,56,Verwaltungsverband,081155002,,Gemeindeverwaltungsverband Gärtringen/Ehningen,Gemeindeverwaltungsverband Gärtringen/Ehningen,Hauptstraße 16 - 18,71116,Gärtringen,,1\n" +
            "08,Baden-Württemberg,60,64,Kreisangehörige Gemeinde,081155002013,08115013,Ehningen,Gemeindeverwaltungsverband Gärtringen/Ehningen,Hauptstraße 16 - 18,71116,Gärtringen,,1\n" +
            "08,Baden-Württemberg,60,64,Kreisangehörige Gemeinde,081155002013,08115013,Ehningen,Gemeindeverwaltung Ehningen,Königstraße 29,71139,Ehningen,,1\n";

        var data = AdministrationAddressParser.Parse(new StringReader(sheet));

        await Assert.That(data.TwoAdministrations).IsEqualTo(2);
        await Assert.That(data.Offices.Select(o => o.Name).ToList()).IsEquivalentTo(["Verwaltungsgemeinschaft Benediktbeuern", "Gemeindeverwaltung Ehningen"]);
    }

    [Test]
    public async Task File_without_header_is_refused() {
        await Assert.That(() => AdministrationAddressParser.Parse(new StringReader("a,b,c\n1,2,3\n"))).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Bundled_directory_covers_lower_saxony() {
        var path = Path.Combine(AppContext.BaseDirectory, AdministrationAddressImportService.FileName);

        var data = AdministrationAddressParser.Parse(new StringReader(CsvTextDecoder.Decode(File.ReadAllBytes(path))));

        var lowerSaxony = data.Offices.Where(o => o.Key.StartsWith("03", StringComparison.Ordinal)).ToList();
        var sottrum = lowerSaxony.Single(o => o.Name == "Samtgemeinde Sottrum");
        await Assert.That(lowerSaxony.Sum(o => o.MunicipalityKeys.Count)).IsEqualTo(941);
        await Assert.That(data.Offices.Sum(o => o.MunicipalityKeys.Count)).IsEqualTo(data.Municipalities);
        await Assert.That(sottrum.MunicipalityKeys).Contains("03357001");
        await Assert.That(sottrum.Street).IsEqualTo("Am Eichkamp 12");
    }
}
