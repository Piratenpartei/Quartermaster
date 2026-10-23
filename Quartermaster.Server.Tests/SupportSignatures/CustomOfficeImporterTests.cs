using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.SupportSignatures;
using Quartermaster.Server.SupportSignatures;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.SupportSignatures;

public class CustomOfficeImporterTests : RepositoryTestBase {
    private const string File =
        "Gemeinde,Gemeindeschluessel,Amt,Zusatz,Strasse_HausNr,PLZ,Ort,Bezeichnung_Liste_2024\n" +
        "Braunschweig,03101000,Stadt Braunschweig,Stelle Wahlen,Reichsstraße 3,38100,Braunschweig,Braunschweig\n" +
        "Salzgitter,03102000,Stadt Salzgitter,Wahlbüro,Joachim-Campe-Straße 6-8,38226,Salzgitter,Salzgitter\n" +
        "Wolfsburg,03103000,Stadt Wolfsburg,,Porschestraße 49,38440,Wolfsburg,Wolfsburg\n";

    private CertificationOfficeRepository _offices = default!;
    private CustomOfficeImporter _importer = default!;
    private AdministrativeDivision _braunschweig = default!;
    private AdministrativeDivision _salzgitter = default!;
    private AdministrativeDivision _wolfsburg = default!;

    [Before(Test)]
    public void Setup() {
        _offices = new CertificationOfficeRepository(Db);
        _importer = new CustomOfficeImporter(_offices, new AdministrativeDivisionRepository(Db), AuditLog);
        _braunschweig = Municipality("Braunschweig", "3101000");
        _salzgitter = Municipality("Salzgitter", "3102000");
        _wolfsburg = Municipality("Wolfsburg", "3103000");
        OfficialOfficeSync.Apply(Db, [
            Official("03101", "Kreisfreie Stadt Braunschweig", "Platz der Deutschen Einheit 1", "38100", "Braunschweig", "03101000"),
            Official("03102", "Kreisfreie Stadt Salzgitter", "Joachim-Campe-Straße 6 - 8", "38226", "Salzgitter", "03102000"),
            Official("03103", "Kreisfreie Stadt Wolfsburg", "Porschestraße 49", "38440", "Wolfsburg", "03103000")
        ]);
    }

    private AdministrativeDivision Municipality(string name, string adminCode) {
        var division = new AdministrativeDivision { Name = name, Depth = AdministrativeDivisionRepository.MunicipalityDepth, AdminCode = adminCode };
        Db.Insert(division);
        return division;
    }

    private static OfficialOfficeEntry Official(string kreis, string name, string street, string postCode, string city, string key) {
        return new OfficialOfficeEntry($"{kreis}|{name}", name, street, postCode, city, null, [key]);
    }

    private CertificationOffice Custom(string name, string? addition, string street, string postCode, string city, params AdministrativeDivision[] municipalities) {
        var office = new CertificationOffice { Name = name, AddressAddition = addition, Street = street, PostCode = postCode, City = city };
        _offices.Create(office, municipalities.Select(m => m.Id));
        return office;
    }

    private (OfficeImportResultDTO? Result, string? ErrorKey) Run(string csv, bool apply) {
        return _importer.Run(Encoding.UTF8.GetBytes(csv), apply);
    }

    [Test]
    public async Task Preview_shows_the_changes_without_storing_them() {
        var (result, _) = Run(File, false);

        await Assert.That(result!.Applied).IsFalse();
        await Assert.That(result.Rows).IsEqualTo(3);
        await Assert.That(result.SameAsOfficial).IsEqualTo(1);
        await Assert.That(result.Entries.Select(e => e.Name).ToList()).IsEquivalentTo(["Stadt Braunschweig", "Stadt Salzgitter"]);
        await Assert.That(result.Entries.All(e => e.IsNew)).IsTrue();
        await Assert.That(_offices.ListCustom()).IsEmpty();
    }

    [Test]
    public async Task Applying_stores_only_rows_that_differ_from_the_official_address() {
        Run(File, true);

        var custom = _offices.ListCustom();
        var assigned = _offices.GetOfficeIdsByMunicipality([_braunschweig.Id, _salzgitter.Id, _wolfsburg.Id]);
        var salzgitter = custom.Single(o => o.Name == "Stadt Salzgitter");
        await Assert.That(custom.Count).IsEqualTo(2);
        await Assert.That(salzgitter.AddressAddition).IsEqualTo("Wahlbüro");
        await Assert.That(assigned[_salzgitter.Id]).IsEqualTo(salzgitter.Id);
        await Assert.That(assigned[_braunschweig.Id]).IsEqualTo(custom.Single(o => o.Name == "Stadt Braunschweig").Id);
        await Assert.That(_offices.Get(assigned[_wolfsburg.Id])!.IsOfficial).IsTrue();
    }

    [Test]
    public async Task Row_matching_the_official_address_drops_an_earlier_custom_office() {
        var earlier = Custom("Stadt Wolfsburg", "Bürgerbüro", "Rathaus 1", "38440", "Wolfsburg", _wolfsburg);

        var (result, _) = Run(File, true);

        await Assert.That(result!.RemovedAssignments).IsEqualTo(1);
        await Assert.That(_offices.Get(earlier.Id)).IsNull();
        await Assert.That(_offices.Get(_offices.GetOfficeIdsByMunicipality([_wolfsburg.Id])[_wolfsburg.Id])!.IsOfficial).IsTrue();
    }

    [Test]
    public async Task File_reuses_a_matching_office_and_overrides_other_assignments() {
        var matching = Custom("Stadt Braunschweig", "Stelle Wahlen", "Reichsstrasse 3", "38100", "Braunschweig");
        var other = Custom("Stadt Salzgitter", null, "Postfach 100680", "38206", "Salzgitter", _salzgitter, _braunschweig);

        var (result, _) = Run(File, true);

        var assigned = _offices.GetOfficeIdsByMunicipality([_braunschweig.Id, _salzgitter.Id]);
        await Assert.That(result!.Entries.Single(e => e.Name == "Stadt Braunschweig").IsNew).IsFalse();
        await Assert.That(assigned[_braunschweig.Id]).IsEqualTo(matching.Id);
        await Assert.That(assigned[_salzgitter.Id]).IsNotEqualTo(other.Id);
        await Assert.That(_offices.Get(other.Id)).IsNull();
        await Assert.That(_offices.ListCustom().Count).IsEqualTo(2);
    }

    [Test]
    public async Task Unknown_keys_are_listed_and_skipped() {
        var csv = "Gemeinde,Gemeindeschluessel,Amt,Strasse_HausNr,PLZ,Ort\nNirgendwo,09999999,Stadt Nirgendwo,Weg 1,99999,Nirgendwo\n";

        var (result, _) = Run(csv, true);

        await Assert.That(result!.UnknownKeys).IsEquivalentTo(["Nirgendwo (09999999)"]);
        await Assert.That(result.Entries).IsEmpty();
        await Assert.That(_offices.ListCustom()).IsEmpty();
    }
}
