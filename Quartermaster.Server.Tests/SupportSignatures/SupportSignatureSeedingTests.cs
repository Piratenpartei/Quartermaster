using System.Linq;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Data.Options;
using Quartermaster.Data.Permissions;
using Quartermaster.Data.Roles;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.SupportSignatures;

public class SupportSignatureSeedingTests : RepositoryTestBase {
    [Test]
    public async Task Officer_role_contains_the_support_signature_permission() {
        var permissions = new PermissionRepository(Db);
        var roles = new RoleRepository(Db);
        permissions.SupplementDefaults();
        roles.SupplementDefaults();

        var permission = permissions.GetByIdentifier(PermissionIdentifier.ManageSupportSignatures);
        var officer = roles.GetByIdentifier(PermissionIdentifier.SystemRole.ChapterOfficer)!;

        await Assert.That(permission).IsNotNull();
        await Assert.That(permission!.Global).IsFalse();
        await Assert.That(roles.GetPermissions(officer.Id)).Contains(PermissionIdentifier.ManageSupportSignatures);
    }

    [Test]
    public async Task Form_size_option_defaults_to_10() {
        var options = new OptionRepository(Db, AuditLog);

        options.SupplementDefaults();

        await Assert.That(options.GetGlobalValue("support_signatures.form_max_megabytes")?.Value).IsEqualTo("10");
    }
}
