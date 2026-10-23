using System;
using System.Linq;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.SupportSignatures;
using Quartermaster.Server.SupportSignatures;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.SupportSignatures;

public class CertificationOfficeSuggesterTests : RepositoryTestBase {
    private CertificationOfficeSuggester _suggester = default!;
    private CertificationOfficeRepository _offices = default!;

    [Before(Test)]
    public void Setup() {
        _offices = new CertificationOfficeRepository(Db);
        _suggester = new CertificationOfficeSuggester(new AdministrativeDivisionRepository(Db), _offices);
        Db.Insert(new MunicipalityAssociation { Key = "033515403", Name = "Lachendorf", Kind = 52, ImportedAt = DateTime.UtcNow });
        Db.Insert(new MunicipalityAssociation { Key = "033510006", Name = "Celle, Stadt", Kind = 50, ImportedAt = DateTime.UtcNow });
    }

    private AdministrativeDivision Municipality(string name, string? associationKey) {
        var division = new AdministrativeDivision { Name = name, Depth = 7, MunicipalityAssociationKey = associationKey };
        Db.Insert(division);
        return division;
    }

    private CertificationOffice Office(string name, params Guid[] municipalityIds) {
        var office = new CertificationOffice { Name = name, Street = "Str. 1", PostCode = "29331", City = "Lachendorf" };
        _offices.Create(office, municipalityIds);
        return office;
    }

    private AdministrativeDivision Coded(string name, string adminCode) {
        var division = new AdministrativeDivision { Name = name, Depth = 7, AdminCode = adminCode, MunicipalityAssociationKey = "033515403" };
        Db.Insert(division);
        return division;
    }

    private void OfficialLachendorf(params string[] keys) {
        OfficialOfficeSync.Apply(Db, [
            new OfficialOfficeEntry("03351|Samtgemeinde Lachendorf", "Samtgemeinde Lachendorf", "Oppershäuser Straße 1", "29331", "Lachendorf", "info@lachendorf.de", keys)
        ]);
    }

    [Test]
    public async Task Official_directory_prefills_name_address_and_all_members() {
        var ahnsbeck = Coded("Ahnsbeck", "3351002");
        var lachendorf = Coded("Lachendorf", "3351016");
        OfficialLachendorf("03351002", "03351016");

        var suggestion = _suggester.Suggest(ahnsbeck.Id)!;

        await Assert.That(suggestion.ExistingOfficeId).IsNull();
        await Assert.That(suggestion.FromOfficialDirectory).IsTrue();
        await Assert.That(suggestion.SuggestedName).IsEqualTo("Samtgemeinde Lachendorf");
        await Assert.That(suggestion.Street).IsEqualTo("Oppershäuser Straße 1");
        await Assert.That(suggestion.PostCode).IsEqualTo("29331");
        await Assert.That(suggestion.Email).IsEqualTo("info@lachendorf.de");
        await Assert.That(suggestion.Municipalities.Select(m => m.Id).ToList()).IsEquivalentTo([ahnsbeck.Id, lachendorf.Id]);
    }

    [Test]
    public async Task Official_member_offers_the_custom_office_a_fellow_member_has() {
        var ahnsbeck = Coded("Ahnsbeck", "3351002");
        var lachendorf = Coded("Lachendorf", "3351016");
        OfficialLachendorf("03351002", "03351016");
        var custom = Office("Samtgemeinde Lachendorf", lachendorf.Id);

        var suggestion = _suggester.Suggest(ahnsbeck.Id)!;

        await Assert.That(suggestion.ExistingOfficeId).IsEqualTo(custom.Id);
        await Assert.That(suggestion.FromOfficialDirectory).IsFalse();
        await Assert.That(suggestion.Municipalities.Select(m => m.Id).ToList()).IsEquivalentTo([ahnsbeck.Id]);
    }

    [Test]
    public async Task Municipality_with_office_returns_it() {
        var ahnsbeck = Municipality("Ahnsbeck", "033515403");
        var office = Office("Samtgemeinde Lachendorf", ahnsbeck.Id);

        var suggestion = _suggester.Suggest(ahnsbeck.Id)!;

        await Assert.That(suggestion.ExistingOfficeId).IsEqualTo(office.Id);
    }

    [Test]
    public async Task Samtgemeinde_member_suggests_the_association_with_all_members() {
        var ahnsbeck = Municipality("Ahnsbeck", "033515403");
        var lachendorf = Municipality("Lachendorf", "033515403");

        var suggestion = _suggester.Suggest(ahnsbeck.Id)!;

        await Assert.That(suggestion.ExistingOfficeId).IsNull();
        await Assert.That(suggestion.SuggestedName).IsEqualTo("Samtgemeinde Lachendorf");
        await Assert.That(suggestion.Municipalities.Select(m => m.Id).ToList()).IsEquivalentTo([ahnsbeck.Id, lachendorf.Id]);
    }

    [Test]
    public async Task Samtgemeinde_member_offers_the_office_a_sibling_already_has() {
        var ahnsbeck = Municipality("Ahnsbeck", "033515403");
        var lachendorf = Municipality("Lachendorf", "033515403");
        var office = Office("Samtgemeinde Lachendorf", lachendorf.Id);

        var suggestion = _suggester.Suggest(ahnsbeck.Id)!;

        await Assert.That(suggestion.ExistingOfficeId).IsEqualTo(office.Id);
        await Assert.That(suggestion.Municipalities.Select(m => m.Id).ToList()).IsEquivalentTo([ahnsbeck.Id]);
    }

    [Test]
    public async Task Municipality_without_association_suggests_itself() {
        var nowhere = Municipality("Kleinstadt", null);

        var suggestion = _suggester.Suggest(nowhere.Id)!;

        await Assert.That(suggestion.SuggestedName).IsEqualTo("Kleinstadt");
        await Assert.That(suggestion.Municipalities.Select(m => m.Id).ToList()).IsEquivalentTo([nowhere.Id]);
    }

    [Test]
    public async Task Einheitsgemeinde_suggests_itself() {
        var celle = Municipality("Celle", "033510006");

        var suggestion = _suggester.Suggest(celle.Id)!;

        await Assert.That(suggestion.SuggestedName).IsEqualTo("Celle");
        await Assert.That(suggestion.Municipalities.Select(m => m.Id).ToList()).IsEquivalentTo([celle.Id]);
    }

    [Test]
    public async Task Non_municipality_gives_no_suggestion() {
        var county = new AdministrativeDivision { Name = "Landkreis Celle", Depth = 6 };
        Db.Insert(county);

        await Assert.That(_suggester.Suggest(county.Id)).IsNull();
    }
}
