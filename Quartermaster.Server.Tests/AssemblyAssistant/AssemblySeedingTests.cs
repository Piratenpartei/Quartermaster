using System;
using System.Linq;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Data;
using Quartermaster.Data.AssemblyAssistant;
using Quartermaster.Data.Options;
using Quartermaster.Data.Permissions;
using Quartermaster.Data.Roles;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.AssemblyAssistant;

public class AssemblySeedingTests : RepositoryTestBase {
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
    public async Task Permissions_are_chapter_scoped() {
        var chair = _permissionRepo.GetByIdentifier(PermissionIdentifier.AssemblyChair);
        var view = _permissionRepo.GetByIdentifier(PermissionIdentifier.AssemblyView);

        await Assert.That(chair).IsNotNull();
        await Assert.That(chair!.Global).IsFalse();
        await Assert.That(view).IsNotNull();
        await Assert.That(view!.Global).IsFalse();
    }

    [Test]
    public async Task Chair_role_has_both_permissions() {
        var chair = _roleRepo.GetByIdentifier(PermissionIdentifier.SystemRole.AssemblyChair);
        await Assert.That(chair).IsNotNull();
        await Assert.That(chair!.IsSystem).IsTrue();
        await Assert.That(_roleRepo.GetPermissions(chair.Id))
            .IsEquivalentTo([PermissionIdentifier.AssemblyChair, PermissionIdentifier.AssemblyView]);

        var minutes = _roleRepo.GetByIdentifier(PermissionIdentifier.SystemRole.AssemblyMinutes);
        await Assert.That(minutes).IsNotNull();
        await Assert.That(_roleRepo.GetPermissions(minutes!.Id)).IsEquivalentTo([PermissionIdentifier.AssemblyView]);
    }

    [Test]
    public async Task Certificate_options_are_secret() {
        var certificate = _optionRepo.GetDefinition("mumble.certificate");
        var password = _optionRepo.GetDefinition("mumble.certificate_password");

        await Assert.That(certificate).IsNotNull();
        await Assert.That(certificate!.IsSecret).IsTrue();
        await Assert.That(password).IsNotNull();
        await Assert.That(password!.IsSecret).IsTrue();
    }

    [Test]
    public async Task Log_entry_round_trips() {
        var chapter = _builder.SeedChapter();
        var evt = _builder.SeedEvent(chapter.Id);
        var startedAt = new DateTime(2026, 11, 28, 9, 0, 0, DateTimeKind.Utc);

        var session = new AssemblySession {
            EventId = evt.Id,
            OpenSlidesMeetingId = 3,
            StartedAt = startedAt,
            StartedByUserId = Guid.NewGuid()
        };
        _context.Insert(session);

        var entry = new AssemblyLogEntry {
            SessionId = session.Id,
            At = startedAt.AddMinutes(5),
            Kind = AssemblyLogKind.Speech,
            ItemTitle = "SÄA001: Beispiel",
            OpenSlidesAgendaItemId = 12,
            MumbleName = "Pirat",
            Role = "Contra",
            StartedAt = startedAt.AddMinutes(2),
            EndedAt = startedAt.AddMinutes(5),
            AllottedSeconds = 180,
            OpenSlidesPollId = 7,
            Detail = "Detail",
            ActorUserId = Guid.NewGuid()
        };
        _context.Insert(entry);

        var loaded = _context.AssemblyLogEntries.Single(e => e.Id == entry.Id);
        await Assert.That(loaded.Kind).IsEqualTo(AssemblyLogKind.Speech);
        await Assert.That(loaded.ItemTitle).IsEqualTo("SÄA001: Beispiel");
        await Assert.That(loaded.StartedAt).IsEqualTo(entry.StartedAt);
        await Assert.That(loaded.AllottedSeconds).IsEqualTo(180);
        await Assert.That(loaded.OpenSlidesPollId).IsEqualTo(7);
        await Assert.That(loaded.ActorUserId).IsEqualTo(entry.ActorUserId);
    }
}
