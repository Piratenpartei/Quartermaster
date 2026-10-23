using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.AdministrativeDivisions;

public class Gv100AdImportTests : RepositoryTestBase {
    private static readonly DateTime ImportedAt = new(2026, 10, 21, 2, 0, 0, DateTimeKind.Utc);

    private AdministrativeDivision SeedMunicipality(string name, string adminCode) {
        var division = new AdministrativeDivision { Name = name, Depth = 7, AdminCode = adminCode };
        Db.Insert(division);
        return division;
    }

    private static Gv100AdData Data() {
        return new Gv100AdData(
            [new Gv100AdAssociation("033515403", "Lachendorf", 52), new Gv100AdAssociation("033510006", "Celle, Stadt", 50)],
            [
                new Gv100AdMunicipality("03351016", "033515403", "Lachendorf"),
                new Gv100AdMunicipality("03351002", "033515403", "Ahnsbeck"),
                new Gv100AdMunicipality("03351006", "033510006", "Celle, Stadt")
            ],
            [],
            []);
    }

    [Test]
    public async Task Applies_keys_by_padded_admin_code() {
        var repository = new AdministrativeDivisionRepository(Db);
        var lachendorf = SeedMunicipality("Lachendorf", "3351016");
        var celle = SeedMunicipality("Celle", "3351006");
        var unknown = SeedMunicipality("Nirgendwo", "9999999");

        var unmatched = repository.ApplyAssociations(Data(), ImportedAt);

        await Assert.That(Db.AdministrativeDivisions.Single(d => d.Id == lachendorf.Id).MunicipalityAssociationKey).IsEqualTo("033515403");
        await Assert.That(Db.AdministrativeDivisions.Single(d => d.Id == celle.Id).MunicipalityAssociationKey).IsEqualTo("033510006");
        await Assert.That(Db.AdministrativeDivisions.Single(d => d.Id == unknown.Id).MunicipalityAssociationKey).IsNull();
        await Assert.That(unmatched).IsEqualTo(1);
        await Assert.That(repository.GetAssociation("033515403")!.Name).IsEqualTo("Lachendorf");
        await Assert.That(repository.GetMunicipalitiesOfAssociation("033515403").Select(d => d.Id).ToList()).IsEquivalentTo([lachendorf.Id]);
    }

    [Test]
    public async Task Reapplying_updates_names_without_duplicates() {
        var repository = new AdministrativeDivisionRepository(Db);
        SeedMunicipality("Lachendorf", "3351016");
        repository.ApplyAssociations(Data(), ImportedAt);
        var renamed = new Gv100AdData([new Gv100AdAssociation("033515403", "Lachendorf (neu)", 52)], [], [], []);

        repository.ApplyAssociations(renamed, ImportedAt.AddDays(1));

        await Assert.That(Db.MunicipalityAssociations.Count(a => a.Key == "033515403")).IsEqualTo(1);
        await Assert.That(repository.GetAssociation("033515403")!.Name).IsEqualTo("Lachendorf (neu)");
    }
}
