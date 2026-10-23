using System;
using System.Linq;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.SupportSignatures;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.SupportSignatures;

public class OfficialOfficeSyncTests : RepositoryTestBase {
    private CertificationOfficeRepository _offices = default!;
    private AdministrativeDivision _bothel = default!;
    private AdministrativeDivision _brockel = default!;
    private AdministrativeDivision _braunschweig = default!;

    [Before(Test)]
    public void Setup() {
        _offices = new CertificationOfficeRepository(Db);
        _bothel = Municipality("Bothel", "3357006");
        _brockel = Municipality("Brockel", "03357009");
        _braunschweig = Municipality("Braunschweig", "3101000");
    }

    private AdministrativeDivision Municipality(string name, string adminCode) {
        var division = new AdministrativeDivision { Name = name, Depth = AdministrativeDivisionRepository.MunicipalityDepth, AdminCode = adminCode };
        Db.Insert(division);
        return division;
    }

    private static OfficialOfficeEntry Bothel(string street = "Horstweg 17", params string[] keys) {
        return new OfficialOfficeEntry("03357|Samtgemeinde Bothel", "Samtgemeinde Bothel", street, "27386", "Bothel", "samtgemeinde@bothel.de",
            keys.Length == 0 ? ["03357006", "03357009"] : keys);
    }

    private static OfficialOfficeEntry Braunschweig(params string[] keys) {
        return new OfficialOfficeEntry("03101|Kreisfreie Stadt Braunschweig", "Kreisfreie Stadt Braunschweig", "Platz der Deutschen Einheit 1", "38100",
            "Braunschweig", null, keys.Length == 0 ? ["03101000"] : keys);
    }

    private CertificationOffice OfficialOffice(string key) {
        return Db.CertificationOffices.Single(o => o.OfficialKey == key);
    }

    [Test]
    public async Task Creates_offices_and_assigns_municipalities_by_their_key() {
        var result = OfficialOfficeSync.Apply(Db, [Bothel(), Braunschweig()]);

        var bothel = OfficialOffice("03357|Samtgemeinde Bothel");
        var assigned = _offices.GetOfficeIdsByMunicipality([_bothel.Id, _brockel.Id, _braunschweig.Id]);
        await Assert.That(result.Created).IsEqualTo(2);
        await Assert.That(result.Assigned).IsEqualTo(3);
        await Assert.That(bothel.IsOfficial).IsTrue();
        await Assert.That(bothel.Street).IsEqualTo("Horstweg 17");
        await Assert.That(assigned[_bothel.Id]).IsEqualTo(bothel.Id);
        await Assert.That(assigned[_brockel.Id]).IsEqualTo(bothel.Id);
        await Assert.That(assigned[_braunschweig.Id]).IsEqualTo(OfficialOffice("03101|Kreisfreie Stadt Braunschweig").Id);
    }

    [Test]
    public async Task New_file_overwrites_addresses_and_assignments_in_place() {
        OfficialOfficeSync.Apply(Db, [Bothel(), Braunschweig()]);
        var before = OfficialOffice("03357|Samtgemeinde Bothel");

        var result = OfficialOfficeSync.Apply(Db, [Bothel("Horstweg 19", "03357006"), Braunschweig("03101000", "03357009")]);

        var after = OfficialOffice("03357|Samtgemeinde Bothel");
        await Assert.That(result.Created).IsEqualTo(0);
        await Assert.That(result.Updated).IsEqualTo(1);
        await Assert.That(after.Id).IsEqualTo(before.Id);
        await Assert.That(after.Street).IsEqualTo("Horstweg 19");
        await Assert.That(_offices.GetOfficeIdsByMunicipality([_brockel.Id])[_brockel.Id]).IsEqualTo(OfficialOffice("03101|Kreisfreie Stadt Braunschweig").Id);
    }

    [Test]
    public async Task Vanished_office_is_deleted_unless_shipments_went_to_it() {
        OfficialOfficeSync.Apply(Db, [Bothel(), Braunschweig()]);
        var chapter = new Chapter { Name = "Niedersachsen" };
        Db.Insert(chapter);
        var campaign = new SignatureCampaign { ChapterId = chapter.Id, Name = "Landesliste", Target = 2000, Deadline = DateTime.UtcNow.AddDays(30), CreatedAt = DateTime.UtcNow };
        Db.Insert(campaign);
        Db.Insert(new SignatureShipment { CampaignId = campaign.Id, OfficeId = OfficialOffice("03101|Kreisfreie Stadt Braunschweig").Id, CreatedAt = DateTime.UtcNow });

        var result = OfficialOfficeSync.Apply(Db, []);

        await Assert.That(result.Removed).IsEqualTo(1);
        await Assert.That(result.KeptForShipments).IsEqualTo(1);
        await Assert.That(Db.CertificationOffices.Select(o => o.OfficialKey).ToList()).IsEquivalentTo(["03101|Kreisfreie Stadt Braunschweig"]);
        await Assert.That(Db.OfficialOfficeMunicipalities.Count()).IsEqualTo(0);
    }

    [Test]
    public async Task Unknown_municipality_keys_are_counted() {
        var result = OfficialOfficeSync.Apply(Db, [Bothel("Horstweg 17", "03357006", "09999999")]);

        await Assert.That(result.Assigned).IsEqualTo(1);
        await Assert.That(result.UnknownMunicipalities).IsEqualTo(1);
    }

    [Test]
    public async Task Hand_entered_office_wins_over_the_official_one() {
        OfficialOfficeSync.Apply(Db, [Bothel()]);
        var custom = new CertificationOffice { Name = "Samtgemeinde Bothel", AddressAddition = "Wahlamt", Street = "Postfach 1", PostCode = "27386", City = "Bothel" };
        _offices.Create(custom, [_brockel.Id]);

        var assigned = _offices.GetOfficeIdsByMunicipality([_bothel.Id, _brockel.Id]);

        await Assert.That(assigned[_brockel.Id]).IsEqualTo(custom.Id);
        await Assert.That(assigned[_bothel.Id]).IsEqualTo(OfficialOffice("03357|Samtgemeinde Bothel").Id);
        await Assert.That(_offices.FindTakenMunicipalities([_bothel.Id], null)).IsEmpty();
        await Assert.That(_offices.ListCustom().Select(o => o.Id).ToList()).IsEquivalentTo([custom.Id]);
    }
}
