using System.Linq;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Data.Permissions;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Permissions;

public class PermissionSeedingTests : RepositoryTestBase {
    [Test]
    public async Task Seeding_renames_an_existing_permission_and_keeps_its_id() {
        var repository = new PermissionRepository(Db);
        repository.SupplementDefaults();
        var seeded = repository.GetByIdentifier(PermissionIdentifier.ViewEvents)!;
        Db.Permissions
            .Where(p => p.Id == seeded.Id)
            .Set(p => p.DisplayName, "Events anzeigen")
            .Update();

        repository.SupplementDefaults();

        var renamed = repository.GetByIdentifier(PermissionIdentifier.ViewEvents)!;
        await Assert.That(renamed.Id).IsEqualTo(seeded.Id);
        await Assert.That(renamed.DisplayName).IsEqualTo("Veranstaltungen anzeigen");
        await Assert.That(Db.Permissions.Count(p => p.Identifier == PermissionIdentifier.ViewEvents)).IsEqualTo(1);
    }
}
