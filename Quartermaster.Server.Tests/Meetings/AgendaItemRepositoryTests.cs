using System.Linq;
using System.Threading.Tasks;
using Quartermaster.Data;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.Meetings;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Meetings;

public class AgendaItemRepositoryTests {
    private WorkerDatabase _db = default!;
    private DbContext _context = default!;
    private TestDataBuilder _builder = default!;
    private AgendaItemRepository _repo = default!;

    [Before(Test)]
    public void Setup() {
        _db = TestDatabaseFixture.Acquire();
        _db.CleanAllTables();
        _context = _db.CreateDbContext();
        _builder = new TestDataBuilder(_context);
        _repo = new AgendaItemRepository(_context, new AuditLogRepository(_context));
    }

    [After(Test)]
    public void Teardown() {
        _context?.Dispose();
        TestDatabaseFixture.Release(_db);
    }

    [Test]
    public async Task Create_appends_to_end_of_siblings() {
        var chapter = _builder.SeedChapter("C");
        var meeting = _builder.SeedMeeting(chapter.Id);
        _repo.Create(new AgendaItem { MeetingId = meeting.Id, Title = "A" });
        _repo.Create(new AgendaItem { MeetingId = meeting.Id, Title = "B" });
        _repo.Create(new AgendaItem { MeetingId = meeting.Id, Title = "C" });

        var items = _repo.GetSiblings(meeting.Id, null);
        await Assert.That(items.Count).IsEqualTo(3);
        await Assert.That(items[0].Title).IsEqualTo("A");
        await Assert.That(items[0].SortOrder).IsEqualTo(0);
        await Assert.That(items[1].SortOrder).IsEqualTo(1);
        await Assert.That(items[2].SortOrder).IsEqualTo(2);
    }

    [Test]
    public async Task Subitems_have_separate_sort_order_namespace_from_parent() {
        var chapter = _builder.SeedChapter("C");
        var meeting = _builder.SeedMeeting(chapter.Id);
        var root = _repo.Create(new AgendaItem { MeetingId = meeting.Id, Title = "Root" });
        _repo.Create(new AgendaItem { MeetingId = meeting.Id, ParentId = root.Id, Title = "Sub1" });
        _repo.Create(new AgendaItem { MeetingId = meeting.Id, ParentId = root.Id, Title = "Sub2" });

        var children = _repo.GetChildren(root.Id);
        await Assert.That(children.Count).IsEqualTo(2);
        await Assert.That(children[0].SortOrder).IsEqualTo(0);
        await Assert.That(children[1].SortOrder).IsEqualTo(1);

        // Root item's SortOrder is also 0 — its sibling namespace is separate.
        await Assert.That(root.SortOrder).IsEqualTo(0);
    }

    [Test]
    public async Task Reorder_swaps_sort_orders_within_siblings() {
        var chapter = _builder.SeedChapter("C");
        var meeting = _builder.SeedMeeting(chapter.Id);
        var a = _repo.Create(new AgendaItem { MeetingId = meeting.Id, Title = "A" });
        var b = _repo.Create(new AgendaItem { MeetingId = meeting.Id, Title = "B" });
        var c = _repo.Create(new AgendaItem { MeetingId = meeting.Id, Title = "C" });

        _repo.Reorder(b.Id, +1); // B should swap with C

        var items = _repo.GetSiblings(meeting.Id, null);
        await Assert.That(items[0].Title).IsEqualTo("A");
        await Assert.That(items[1].Title).IsEqualTo("C");
        await Assert.That(items[2].Title).IsEqualTo("B");
    }

    [Test]
    public async Task Reorder_ignored_when_at_boundary() {
        var chapter = _builder.SeedChapter("C");
        var meeting = _builder.SeedMeeting(chapter.Id);
        var a = _repo.Create(new AgendaItem { MeetingId = meeting.Id, Title = "A" });
        var b = _repo.Create(new AgendaItem { MeetingId = meeting.Id, Title = "B" });

        _repo.Reorder(a.Id, -1); // A is first, can't move up

        var items = _repo.GetSiblings(meeting.Id, null);
        await Assert.That(items[0].Title).IsEqualTo("A");
        await Assert.That(items[1].Title).IsEqualTo("B");
    }

    [Test]
    public async Task GetDepth_root_item_returns_1() {
        var chapter = _builder.SeedChapter("C");
        var meeting = _builder.SeedMeeting(chapter.Id);
        var root = _repo.Create(new AgendaItem { MeetingId = meeting.Id, Title = "Root" });
        await Assert.That(_repo.GetDepth(root.Id)).IsEqualTo(1);
    }

    [Test]
    public async Task GetDepth_nested_items_return_correct_depth() {
        var chapter = _builder.SeedChapter("C");
        var meeting = _builder.SeedMeeting(chapter.Id);
        var root = _repo.Create(new AgendaItem { MeetingId = meeting.Id, Title = "Root" });
        var sub = _repo.Create(new AgendaItem { MeetingId = meeting.Id, ParentId = root.Id, Title = "Sub" });
        var subsub = _repo.Create(new AgendaItem { MeetingId = meeting.Id, ParentId = sub.Id, Title = "SubSub" });
        await Assert.That(_repo.GetDepth(sub.Id)).IsEqualTo(2);
        await Assert.That(_repo.GetDepth(subsub.Id)).IsEqualTo(3);
    }

    [Test]
    public async Task WouldCreateCycle_returns_true_for_self() {
        var chapter = _builder.SeedChapter("C");
        var meeting = _builder.SeedMeeting(chapter.Id);
        var root = _repo.Create(new AgendaItem { MeetingId = meeting.Id, Title = "Root" });
        await Assert.That(_repo.WouldCreateCycle(root.Id, root.Id)).IsTrue();
    }

    [Test]
    public async Task WouldCreateCycle_returns_true_for_descendant() {
        var chapter = _builder.SeedChapter("C");
        var meeting = _builder.SeedMeeting(chapter.Id);
        var root = _repo.Create(new AgendaItem { MeetingId = meeting.Id, Title = "Root" });
        var sub = _repo.Create(new AgendaItem { MeetingId = meeting.Id, ParentId = root.Id, Title = "Sub" });
        // Moving root under its own child = cycle
        await Assert.That(_repo.WouldCreateCycle(root.Id, sub.Id)).IsTrue();
    }

    [Test]
    public async Task WouldCreateCycle_returns_false_for_unrelated_branch() {
        var chapter = _builder.SeedChapter("C");
        var meeting = _builder.SeedMeeting(chapter.Id);
        var a = _repo.Create(new AgendaItem { MeetingId = meeting.Id, Title = "A" });
        var b = _repo.Create(new AgendaItem { MeetingId = meeting.Id, Title = "B" });
        await Assert.That(_repo.WouldCreateCycle(a.Id, b.Id)).IsFalse();
    }

    [Test]
    public async Task Move_reparents_and_appends_to_new_siblings() {
        var chapter = _builder.SeedChapter("C");
        var meeting = _builder.SeedMeeting(chapter.Id);
        var parent1 = _repo.Create(new AgendaItem { MeetingId = meeting.Id, Title = "P1" });
        var parent2 = _repo.Create(new AgendaItem { MeetingId = meeting.Id, Title = "P2" });
        var child = _repo.Create(new AgendaItem { MeetingId = meeting.Id, ParentId = parent1.Id, Title = "C" });
        _repo.Create(new AgendaItem { MeetingId = meeting.Id, ParentId = parent2.Id, Title = "X" });

        _repo.Move(child.Id, parent2.Id);

        var parent1Children = _repo.GetChildren(parent1.Id);
        var parent2Children = _repo.GetChildren(parent2.Id);
        await Assert.That(parent1Children.Count).IsEqualTo(0);
        await Assert.That(parent2Children.Count).IsEqualTo(2);
        await Assert.That(parent2Children.Any(c => c.Title == "C")).IsTrue();
    }

    [Test]
    public async Task Delete_cascades_to_children_via_FK() {
        var chapter = _builder.SeedChapter("C");
        var meeting = _builder.SeedMeeting(chapter.Id);
        var root = _repo.Create(new AgendaItem { MeetingId = meeting.Id, Title = "Root" });
        _repo.Create(new AgendaItem { MeetingId = meeting.Id, ParentId = root.Id, Title = "Sub" });
        _repo.Delete(root.Id);
        var remaining = _repo.GetForMeeting(meeting.Id);
        await Assert.That(remaining.Count).IsEqualTo(0);
    }

    [Test]
    public async Task MarkStarted_and_MarkCompleted_set_timestamps() {
        var chapter = _builder.SeedChapter("C");
        var meeting = _builder.SeedMeeting(chapter.Id);
        var item = _repo.Create(new AgendaItem { MeetingId = meeting.Id, Title = "X" });
        _repo.MarkStarted(item.Id);
        var started = _repo.Get(item.Id)!;
        await Assert.That(started.StartedAt).IsNotNull();
        await Assert.That(started.CompletedAt).IsNull();
        _repo.MarkCompleted(item.Id);
        var completed = _repo.Get(item.Id)!;
        await Assert.That(completed.CompletedAt).IsNotNull();
    }

    [Test]
    public async Task CompleteAllInProgressExcept_finishes_other_active_items() {
        var chapter = _builder.SeedChapter("C");
        var meeting = _builder.SeedMeeting(chapter.Id);
        var a = _repo.Create(new AgendaItem { MeetingId = meeting.Id, Title = "A" });
        var b = _repo.Create(new AgendaItem { MeetingId = meeting.Id, Title = "B" });
        _repo.MarkStarted(a.Id);
        _repo.MarkStarted(b.Id);

        _repo.CompleteAllInProgressExcept(meeting.Id, b.Id);

        var fetchedA = _repo.Get(a.Id)!;
        var fetchedB = _repo.Get(b.Id)!;
        await Assert.That(fetchedA.CompletedAt).IsNotNull();
        await Assert.That(fetchedB.CompletedAt).IsNull();
    }

    [Test]
    public async Task UpdateNotes_only_updates_notes_field() {
        var chapter = _builder.SeedChapter("C");
        var meeting = _builder.SeedMeeting(chapter.Id);
        var item = _repo.Create(new AgendaItem { MeetingId = meeting.Id, Title = "X" });
        _repo.UpdateNotes(item.Id, "my notes");
        var fetched = _repo.Get(item.Id)!;
        await Assert.That(fetched.Notes).IsEqualTo("my notes");
        await Assert.That(fetched.Title).IsEqualTo("X");
    }
}
