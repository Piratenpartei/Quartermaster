using System;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Meetings;
using Quartermaster.Data;
using Quartermaster.Data.Meetings;
using Quartermaster.Data.Permissions;
using Quartermaster.Data.Roles;
using Quartermaster.Server.Meetings;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Meetings;

public class MeetingAccessHelperTests {
    private WorkerDatabase _db = default!;
    private DbContext _context = default!;
    private TestDataBuilder _builder = default!;
    private RoleRepository _roleRepo = default!;

    [Before(Test)]
    public void Setup() {
        _db = TestDatabaseFixture.Acquire();
        _db.CleanAllTables();
        _context = _db.CreateDbContext();
        _builder = new TestDataBuilder(_context);
        var permRepo = new PermissionRepository(_context);
        permRepo.SupplementDefaults();
        _roleRepo = new RoleRepository(_context);
        _roleRepo.SupplementDefaults();
    }

    [After(Test)]
    public void Teardown() {
        _context?.Dispose();
        TestDatabaseFixture.Release(_db);
    }

    [Test]
    public async Task Public_meeting_visible_to_anonymous() {
        var chapter = _builder.SeedChapter("C");
        var meeting = _builder.SeedMeeting(chapter.Id, visibility: MeetingVisibility.Public);
        var result = MeetingAccessHelper.CanUserViewMeeting(null, meeting, _roleRepo);
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task Public_meeting_visible_to_unrelated_user() {
        var chapter = _builder.SeedChapter("C");
        var meeting = _builder.SeedMeeting(chapter.Id, visibility: MeetingVisibility.Public);
        var (user, _) = _builder.SeedAuthenticatedUser();
        var result = MeetingAccessHelper.CanUserViewMeeting(user.Id, meeting, _roleRepo);
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task Private_meeting_denied_to_anonymous() {
        var chapter = _builder.SeedChapter("C");
        var meeting = _builder.SeedMeeting(chapter.Id, visibility: MeetingVisibility.Private);
        var result = MeetingAccessHelper.CanUserViewMeeting(null, meeting, _roleRepo);
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task Private_meeting_denied_to_unrelated_user() {
        var chapter = _builder.SeedChapter("C");
        var meeting = _builder.SeedMeeting(chapter.Id, visibility: MeetingVisibility.Private);
        var (user, _) = _builder.SeedAuthenticatedUser();
        var result = MeetingAccessHelper.CanUserViewMeeting(user.Id, meeting, _roleRepo);
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task Private_meeting_allowed_to_direct_officer() {
        var chapter = _builder.SeedChapter("C");
        var meeting = _builder.SeedMeeting(chapter.Id, visibility: MeetingVisibility.Private);
        var (user, _) = _builder.SeedAuthenticatedUser();
        var officerRole = _roleRepo.GetByIdentifier(PermissionIdentifier.SystemRole.ChapterOfficer)!;
        _roleRepo.Assign(user.Id, officerRole.Id, chapter.Id);
        var result = MeetingAccessHelper.CanUserViewMeeting(user.Id, meeting, _roleRepo);
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task Private_meeting_allowed_to_direct_delegate() {
        var chapter = _builder.SeedChapter("C");
        var meeting = _builder.SeedMeeting(chapter.Id, visibility: MeetingVisibility.Private);
        var (user, _) = _builder.SeedAuthenticatedUser();
        var delegateRole = _roleRepo.GetByIdentifier(PermissionIdentifier.SystemRole.GeneralChapterDelegate)!;
        _roleRepo.Assign(user.Id, delegateRole.Id, chapter.Id);
        var result = MeetingAccessHelper.CanUserViewMeeting(user.Id, meeting, _roleRepo);
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task Private_meeting_denied_to_officer_of_parent_chapter() {
        // Officer of parent chapter normally inherits view permissions into children,
        // but private meeting access is direct-only — not inherited.
        var chain = _builder.SeedChapterHierarchy("Parent", "Child");
        var meeting = _builder.SeedMeeting(chain[1].Id, visibility: MeetingVisibility.Private);
        var (user, _) = _builder.SeedAuthenticatedUser();
        var officerRole = _roleRepo.GetByIdentifier(PermissionIdentifier.SystemRole.ChapterOfficer)!;
        _roleRepo.Assign(user.Id, officerRole.Id, chain[0].Id);
        var result = MeetingAccessHelper.CanUserViewMeeting(user.Id, meeting, _roleRepo);
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task Private_meeting_denied_to_officer_of_child_chapter() {
        // Officer of child chapter also can't see parent's private meeting.
        var chain = _builder.SeedChapterHierarchy("Parent", "Child");
        var meeting = _builder.SeedMeeting(chain[0].Id, visibility: MeetingVisibility.Private);
        var (user, _) = _builder.SeedAuthenticatedUser();
        var officerRole = _roleRepo.GetByIdentifier(PermissionIdentifier.SystemRole.ChapterOfficer)!;
        _roleRepo.Assign(user.Id, officerRole.Id, chain[1].Id);
        var result = MeetingAccessHelper.CanUserViewMeeting(user.Id, meeting, _roleRepo);
        await Assert.That(result).IsFalse();
    }
}
