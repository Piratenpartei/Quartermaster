using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Events;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Roles;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Events;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Events;

public class EventUserVisibilityTests : RepositoryTestBase {
    private EventUserVisibility _visibility = default!;
    private TestDataBuilder _builder = default!;

    [Before(Test)]
    public void Setup() {
        var roles = new RoleRepository(Db);
        _visibility = new EventUserVisibility(
            new UserGlobalPermissionRepository(Db, roles),
            new UserChapterPermissionRepository(Db, roles),
            new ChapterRepository(Db, AuditLog));
        _builder = new TestDataBuilder(Db);
    }

    [Test]
    [Arguments(EventVisibility.Public)]
    [Arguments(EventVisibility.MembersOnly)]
    public async Task Open_events_are_visible_to_any_user(EventVisibility visibility) {
        var ev = _builder.SeedEvent(_builder.SeedChapter().Id, visibility: visibility);
        var user = _builder.SeedUser();

        await Assert.That(_visibility.IsVisibleTo(ev, user.Id)).IsTrue();
    }

    [Test]
    public async Task Private_needs_ViewEvents_on_chapter() {
        var chapter = _builder.SeedChapter();
        var ev = _builder.SeedEvent(chapter.Id, visibility: EventVisibility.Private);
        var without = _builder.SeedUser();
        var with = _builder.SeedUser();
        _builder.GrantChapterPermission(with.Id, chapter.Id, PermissionIdentifier.ViewEvents);

        await Assert.That(_visibility.IsVisibleTo(ev, without.Id)).IsFalse();
        await Assert.That(_visibility.IsVisibleTo(ev, with.Id)).IsTrue();
    }

    [Test]
    public async Task Private_visible_with_inherited_ViewEvents() {
        var parent = _builder.SeedChapter("Parent");
        var child = _builder.SeedChapter("Child", parentChapterId: parent.Id);
        var ev = _builder.SeedEvent(child.Id, visibility: EventVisibility.Private);
        var user = _builder.SeedUser();
        _builder.GrantChapterPermission(user.Id, parent.Id, PermissionIdentifier.ViewEvents);

        await Assert.That(_visibility.IsVisibleTo(ev, user.Id)).IsTrue();
    }

    [Test]
    public async Task Private_visible_with_global_ViewEvents() {
        var ev = _builder.SeedEvent(_builder.SeedChapter().Id, visibility: EventVisibility.Private);
        var user = _builder.SeedUser();
        _builder.GrantGlobalPermission(user.Id, PermissionIdentifier.ViewEvents);

        await Assert.That(_visibility.IsVisibleTo(ev, user.Id)).IsTrue();
    }
}
