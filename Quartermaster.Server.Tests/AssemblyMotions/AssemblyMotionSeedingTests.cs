using System;
using System.Linq;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Data;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Options;
using Quartermaster.Data.Permissions;
using Quartermaster.Data.Roles;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.AssemblyMotions;

public class AssemblyMotionSeedingTests : RepositoryTestBase {
    private DbContext _context = default!;
    private TestDataBuilder _builder = default!;
    private PermissionRepository _permissionRepo = default!;
    private RoleRepository _roleRepo = default!;
    private OptionRepository _optionRepo = default!;

    [Before(Test)]
    public void Setup() {
        _context = Db;
        _builder = new TestDataBuilder(_context);
        _permissionRepo = new PermissionRepository(_context);
        _roleRepo = new RoleRepository(_context);
        _optionRepo = new OptionRepository(_context, AuditLog);

        _permissionRepo.SupplementDefaults();
        _roleRepo.SupplementDefaults();
        _optionRepo.SupplementDefaults();
    }

    [Test]
    public async Task Permission_is_seeded_chapter_scoped() {
        var permission = _permissionRepo.GetByIdentifier(PermissionIdentifier.ManageAssemblyMotions);
        await Assert.That(permission).IsNotNull();
        await Assert.That(permission!.Global).IsFalse();
    }

    [Test]
    public async Task Motion_commission_role_contains_permission() {
        var role = _roleRepo.GetByIdentifier(PermissionIdentifier.SystemRole.MotionCommission);
        await Assert.That(role).IsNotNull();
        await Assert.That(role!.IsSystem).IsTrue();

        var perms = _roleRepo.GetPermissions(role.Id);
        await Assert.That(perms).IsEquivalentTo([PermissionIdentifier.ManageAssemblyMotions]);
    }

    [Test]
    public async Task Wiki_password_option_is_secret() {
        var wikiPassword = _optionRepo.GetDefinition("wiki.bot_password");
        await Assert.That(wikiPassword).IsNotNull();
        await Assert.That(wikiPassword!.IsSecret).IsTrue();

        var openSlidesPassword = _optionRepo.GetDefinition("openslides.password");
        await Assert.That(openSlidesPassword).IsNotNull();
        await Assert.That(openSlidesPassword!.IsSecret).IsTrue();
    }

    [Test]
    public async Task Motion_number_is_unique_per_type() {
        var chapter = _builder.SeedChapter();
        var evt = _builder.SeedEvent(chapter.Id);
        var member = _builder.SeedMember(chapter.Id);

        var portal = new AssemblyMotionPortal {
            EventId = evt.Id,
            OpensAt = DateTime.UtcNow,
            ClosesAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };
        _context.Insert(portal);

        var type = new AssemblyMotionType {
            PortalId = portal.Id,
            Code = "SA",
            Name = "Sachantrag",
            SortOrder = 0,
            MinSubmitters = 1
        };
        _context.Insert(type);

        _context.Insert(NewMotion(portal.Id, type.Id, member.Id, 1));

        await Assert.ThrowsAsync<MySqlConnector.MySqlException>(
            () => Task.Run(() => _context.Insert(NewMotion(portal.Id, type.Id, member.Id, 1))));

        _context.Insert(NewMotion(portal.Id, type.Id, member.Id, null));
        _context.Insert(NewMotion(portal.Id, type.Id, member.Id, null));

        await Assert.That(_context.AssemblyMotions.Count(m => m.PortalId == portal.Id)).IsEqualTo(3);
    }

    private static AssemblyMotion NewMotion(Guid portalId, Guid typeId, Guid createdByMemberId, int? number) => new() {
        PortalId = portalId,
        TypeId = typeId,
        Number = number,
        Title = "Testantrag",
        CreatedByMemberId = createdByMemberId,
        CreatedAt = DateTime.UtcNow
    };
}
