using System;
using System.Threading.Tasks;
using Quartermaster.Api.Meetings;
using Quartermaster.Data;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.Meetings;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Meetings;

public class MeetingRepositoryTests {
    private WorkerDatabase _db = default!;
    private DbContext _context = default!;
    private TestDataBuilder _builder = default!;
    private MeetingRepository _repo = default!;

    [Before(Test)]
    public void Setup() {
        _db = TestDatabaseFixture.Acquire();
        _db.CleanAllTables();
        _context = _db.CreateDbContext();
        _builder = new TestDataBuilder(_context);
        _repo = new MeetingRepository(_context, new AuditLogRepository(_context));
    }

    [After(Test)]
    public void Teardown() {
        _context?.Dispose();
        TestDatabaseFixture.Release(_db);
    }

    [Test]
    public async Task Create_assigns_id_and_timestamp() {
        var chapter = _builder.SeedChapter("C");
        var meeting = new Meeting {
            ChapterId = chapter.Id,
            Title = "Test",
            Status = MeetingStatus.Draft,
            Visibility = MeetingVisibility.Private
        };
        _repo.Create(meeting);
        await Assert.That(meeting.Id).IsNotEqualTo(Guid.Empty);
        await Assert.That(meeting.CreatedAt).IsGreaterThan(DateTime.UtcNow.AddMinutes(-1));
    }

    [Test]
    public async Task Get_returns_null_for_soft_deleted() {
        var chapter = _builder.SeedChapter("C");
        var m = _builder.SeedMeeting(chapter.Id);
        _repo.SoftDelete(m.Id);
        var fetched = _repo.Get(m.Id);
        await Assert.That(fetched).IsNull();
    }

    [Test]
    public async Task List_filters_by_chapter() {
        var chapterA = _builder.SeedChapter("A");
        var chapterB = _builder.SeedChapter("B");
        _builder.SeedMeeting(chapterA.Id);
        _builder.SeedMeeting(chapterA.Id);
        _builder.SeedMeeting(chapterB.Id);
        var (items, total) = _repo.List(chapterA.Id, null, null, null, null, null, 1, 10);
        await Assert.That(total).IsEqualTo(2);
        await Assert.That(items.Count).IsEqualTo(2);
    }

    [Test]
    public async Task List_filters_by_status() {
        var chapter = _builder.SeedChapter("C");
        _builder.SeedMeeting(chapter.Id, status: MeetingStatus.Draft);
        _builder.SeedMeeting(chapter.Id, status: MeetingStatus.InProgress);
        _builder.SeedMeeting(chapter.Id, status: MeetingStatus.Archived);
        var (items, _) = _repo.List(null, MeetingStatus.InProgress, null, null, null, null, 1, 10);
        await Assert.That(items.Count).IsEqualTo(1);
        await Assert.That(items[0].Status).IsEqualTo(MeetingStatus.InProgress);
    }

    [Test]
    public async Task List_filters_by_visibility() {
        var chapter = _builder.SeedChapter("C");
        _builder.SeedMeeting(chapter.Id, visibility: MeetingVisibility.Public);
        _builder.SeedMeeting(chapter.Id, visibility: MeetingVisibility.Private);
        var (items, _) = _repo.List(null, null, MeetingVisibility.Public, null, null, null, 1, 10);
        await Assert.That(items.Count).IsEqualTo(1);
        await Assert.That(items[0].Visibility).IsEqualTo(MeetingVisibility.Public);
    }

    [Test]
    public async Task List_respects_restrictToChapterIds() {
        var chapterA = _builder.SeedChapter("A");
        var chapterB = _builder.SeedChapter("B");
        var chapterC = _builder.SeedChapter("C");
        _builder.SeedMeeting(chapterA.Id);
        _builder.SeedMeeting(chapterB.Id);
        _builder.SeedMeeting(chapterC.Id);
        var (items, _) = _repo.List(null, null, null, null, null,
            new() { chapterA.Id, chapterB.Id }, 1, 10);
        await Assert.That(items.Count).IsEqualTo(2);
    }

    [Test]
    public async Task UpdateStatus_sets_StartedAt_on_InProgress_transition() {
        var chapter = _builder.SeedChapter("C");
        var m = _builder.SeedMeeting(chapter.Id, status: MeetingStatus.Scheduled);
        _repo.UpdateStatus(m.Id, MeetingStatus.InProgress);
        var fetched = _repo.Get(m.Id)!;
        await Assert.That(fetched.Status).IsEqualTo(MeetingStatus.InProgress);
        await Assert.That(fetched.StartedAt).IsNotNull();
    }

    [Test]
    public async Task UpdateStatus_sets_CompletedAt_on_Completed_transition() {
        var chapter = _builder.SeedChapter("C");
        var m = _builder.SeedMeeting(chapter.Id, status: MeetingStatus.InProgress);
        _repo.UpdateStatus(m.Id, MeetingStatus.Completed);
        var fetched = _repo.Get(m.Id)!;
        await Assert.That(fetched.Status).IsEqualTo(MeetingStatus.Completed);
        await Assert.That(fetched.CompletedAt).IsNotNull();
    }

    [Test]
    public async Task List_paginates() {
        var chapter = _builder.SeedChapter("C");
        for (var i = 0; i < 12; i++)
            _builder.SeedMeeting(chapter.Id, title: $"M{i}");
        var (page1, total) = _repo.List(null, null, null, null, null, null, 1, 5);
        var (page2, _) = _repo.List(null, null, null, null, null, null, 2, 5);
        await Assert.That(total).IsEqualTo(12);
        await Assert.That(page1.Count).IsEqualTo(5);
        await Assert.That(page2.Count).IsEqualTo(5);
    }
}
