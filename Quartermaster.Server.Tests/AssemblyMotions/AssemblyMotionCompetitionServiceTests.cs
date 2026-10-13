using System;
using System.Linq;
using System.Threading.Tasks;
using LinqToDB;
using Microsoft.Extensions.Logging.Abstractions;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Data;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Events;
using Quartermaster.Data.Members;
using Quartermaster.Server.AssemblyMotions;
using Quartermaster.Server.AssemblyMotions.Notifications;
using Quartermaster.Server.Notifications;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.AssemblyMotions;

public class AssemblyMotionCompetitionServiceTests : RepositoryTestBase {
    private readonly CapturingNotificationQueue _queue = new();
    private readonly Guid _userId = Guid.NewGuid();
    private DateTime _now = DateTime.UtcNow;
    private TestDataBuilder _builder = default!;
    private AssemblyMotionPortalRepository _portalRepo = default!;
    private AssemblyMotionRepository _motionRepo = default!;
    private AssemblyMotionCompetitionRepository _competitionRepo = default!;
    private AssemblyMotionService _service = default!;
    private AssemblyMotionCompetitionService _competitions = default!;
    private Chapter _chapter = default!;
    private AssemblyMotionPortal _portal = default!;

    [Before(Test)]
    public void Setup() {
        _builder = new TestDataBuilder(Db);
        _portalRepo = new AssemblyMotionPortalRepository(Db, AuditLog);
        _motionRepo = new AssemblyMotionRepository(Db, AuditLog);
        _competitionRepo = new AssemblyMotionCompetitionRepository(Db);
        var notifier = new AssemblyMotionNotifier(_queue, Db, NullLogger<AssemblyMotionNotifier>.Instance);
        _service = new AssemblyMotionService(_motionRepo, _portalRepo, AuditLog, Db, notifier);
        _competitions = new AssemblyMotionCompetitionService(_motionRepo, _competitionRepo, _portalRepo, _service, AuditLog, notifier);
        _chapter = _builder.SeedChapter();
        var ev = _builder.SeedEvent(_chapter.Id);
        _portal = _portalRepo.Create(ev.Id, _now.AddHours(-1), _now.AddDays(7), _now);
        Db.AssemblyMotionPortals
            .Where(p => p.Id == _portal.Id)
            .Set(p => p.WikiMirrorEnabled, true)
            .Update();
    }

    private (AssemblyMotion Motion, Member Author) NewMotion(bool release, int minSubmitters = 1) {
        _now = _now.AddSeconds(1);
        var type = _portalRepo.GetTypes(_portal.Id).First(t => t.MinSubmitters == minSubmitters);
        var author = _builder.SeedMember(chapterId: _chapter.Id);
        var draft = new AssemblyMotionDraft(type.Id, "Testantrag", null, "Antragstext", "Begründung", null, null, null);
        var motion = _service.CreateDraft(_portal, author, _userId, draft, _now).Motion!;
        if (release)
            motion = _service.Release(motion.Id, author, _now).Motion!;
        return (motion, author);
    }

    private (AssemblyMotion Motion, Member Author) Submitted() => NewMotion(true);

    private static AssemblyMotionNotificationPayload PayloadOf(NotificationDispatchRequest request)
        => (AssemblyMotionNotificationPayload)request.Payload;

    private AssemblyMotionCompetition OnlyLink(Guid motionId) => _competitionRepo.ListForMotion(motionId).Single();

    [Test]
    public async Task Declare_is_symmetric() {
        var (first, firstAuthor) = Submitted();
        var (second, _) = Submitted();

        var outcome = _competitions.Declare(second.Id, first.Id, _userId, firstAuthor, true, _now);
        var forFirst = _competitionRepo.ListForMotion(first.Id);
        var forSecond = _competitionRepo.ListForMotion(second.Id);

        await Assert.That(outcome.Succeeded).IsTrue();
        await Assert.That(forFirst.Single().Id).IsEqualTo(forSecond.Single().Id);
        await Assert.That(_competitionRepo.ListForMotions([first.Id, second.Id]).Values.All(l => l.Count == 1)).IsTrue();
    }

    [Test]
    public async Task Pair_stored_ordered_and_unique() {
        var (first, firstAuthor) = Submitted();
        var (second, secondAuthor) = Submitted();

        var forward = _competitions.Declare(first.Id, second.Id, _userId, firstAuthor, false, _now);
        var backward = _competitions.Declare(second.Id, first.Id, _userId, secondAuthor, false, _now);
        var link = OnlyLink(first.Id);
        var pair = AssemblyMotionCompetitionService.OrderPair(first.Id, second.Id);

        await Assert.That(forward.Succeeded).IsTrue();
        await Assert.That(backward.Succeeded).IsFalse();
        await Assert.That(backward.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.CompetitionExists);
        await Assert.That(link.MotionAId).IsEqualTo(pair.A);
        await Assert.That(link.MotionBId).IsEqualTo(pair.B);
        await Assert.That(link.DeclaringMotionId).IsEqualTo(first.Id);
        await Assert.That(link.DeclaredByCommission).IsFalse();
    }

    [Test]
    public async Task Declare_against_collecting_motion_refused() {
        var (submitted, author) = Submitted();
        var (collecting, _) = NewMotion(true, minSubmitters: 5);

        var outcome = _competitions.Declare(submitted.Id, collecting.Id, _userId, author, false, _now);

        await Assert.That(_motionRepo.Get(collecting.Id)!.Status).IsEqualTo(AssemblyMotionStatus.CollectingCoSubmitters);
        await Assert.That(outcome.Succeeded).IsFalse();
        await Assert.That(outcome.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.InvalidState);
    }

    [Test]
    public async Task Declare_requires_submitter_of_declaring_motion_unless_commission() {
        var (first, _) = Submitted();
        var (second, _) = Submitted();
        var stranger = _builder.SeedMember(chapterId: _chapter.Id);

        var refused = _competitions.Declare(first.Id, second.Id, _userId, stranger, false, _now);
        var byCommission = _competitions.Declare(first.Id, second.Id, _userId, null, true, _now);

        await Assert.That(refused.StatusCode).IsEqualTo(403);
        await Assert.That(byCommission.Succeeded).IsTrue();
        await Assert.That(OnlyLink(first.Id).DeclaredByCommission).IsTrue();
    }

    [Test]
    public async Task Declare_refused_for_same_motion_and_other_portal() {
        var (first, author) = Submitted();
        var otherEvent = _builder.SeedEvent(_chapter.Id);
        var otherPortal = _portalRepo.Create(otherEvent.Id, _now.AddHours(-1), _now.AddDays(7), _now);
        var otherType = _portalRepo.GetTypes(otherPortal.Id).First(t => t.MinSubmitters == 1);
        var otherAuthor = _builder.SeedMember(chapterId: _chapter.Id);
        var foreign = _service.CreateDraft(otherPortal, otherAuthor, _userId,
            new AssemblyMotionDraft(otherType.Id, "Fremd", null, "Text", "Grund", null, null, null), _now).Motion!;
        _service.Release(foreign.Id, otherAuthor, _now);

        var self = _competitions.Declare(first.Id, first.Id, _userId, author, false, _now);
        var crossPortal = _competitions.Declare(first.Id, foreign.Id, _userId, author, false, _now);

        await Assert.That(self.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.InvalidState);
        await Assert.That(crossPortal.StatusCode).IsEqualTo(404);
        await Assert.That(crossPortal.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.NotFound);
        await Assert.That(_competitionRepo.ListForMotion(first.Id).Count).IsEqualTo(0);
    }

    [Test]
    public async Task Commission_link_removable_only_by_commission() {
        var (first, firstAuthor) = Submitted();
        var (second, _) = Submitted();
        _competitions.Declare(first.Id, second.Id, _userId, null, true, _now);
        var link = OnlyLink(first.Id);

        var bySubmitter = _competitions.Remove(link.Id, _userId, firstAuthor, false, _now);
        var byCommission = _competitions.Remove(link.Id, _userId, null, true, _now);

        await Assert.That(bySubmitter.StatusCode).IsEqualTo(403);
        await Assert.That(byCommission.Succeeded).IsTrue();
        await Assert.That(_competitionRepo.ListForMotion(first.Id).Count).IsEqualTo(0);
    }

    [Test]
    public async Task Submitter_link_removable_by_declaring_submitter_not_by_other_side() {
        var (first, firstAuthor) = Submitted();
        var (second, secondAuthor) = Submitted();
        _competitions.Declare(first.Id, second.Id, _userId, firstAuthor, false, _now);
        var link = OnlyLink(first.Id);

        var byOtherSide = _competitions.Remove(link.Id, _userId, secondAuthor, false, _now);
        var byDeclaring = _competitions.Remove(link.Id, _userId, firstAuthor, false, _now);

        await Assert.That(byOtherSide.StatusCode).IsEqualTo(403);
        await Assert.That(byDeclaring.Succeeded).IsTrue();
        await Assert.That(_competitionRepo.Get(link.Id)).IsNull();
    }

    [Test]
    public async Task Flag_requires_earlier_submitted_link() {
        var (first, firstAuthor) = Submitted();
        var (second, secondAuthor) = Submitted();

        var withoutLink = _competitions.SetCompetingFlag(second.Id, true, secondAuthor, false, _now);
        _competitions.Declare(second.Id, first.Id, _userId, secondAuthor, false, _now);
        var earlierSide = _competitions.SetCompetingFlag(first.Id, true, firstAuthor, false, _now);
        var laterSide = _competitions.SetCompetingFlag(second.Id, true, secondAuthor, false, _now);

        await Assert.That(withoutLink.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.CompetingFlagRequiresLink);
        await Assert.That(earlierSide.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.CompetingFlagRequiresLink);
        await Assert.That(laterSide.Succeeded).IsTrue();
        await Assert.That(_motionRepo.Get(second.Id)!.IsCompetingMotion).IsTrue();
        await Assert.That(_motionRepo.Get(first.Id)!.IsCompetingMotion).IsFalse();
    }

    [Test]
    public async Task Flag_is_author_only_and_commission_can_only_clear() {
        var (first, _) = Submitted();
        var (second, secondAuthor) = Submitted();
        var stranger = _builder.SeedMember(chapterId: _chapter.Id);
        _competitions.Declare(second.Id, first.Id, _userId, secondAuthor, false, _now);

        var byStranger = _competitions.SetCompetingFlag(second.Id, true, stranger, false, _now);
        var commissionSets = _competitions.SetCompetingFlag(second.Id, true, null, true, _now);
        _competitions.SetCompetingFlag(second.Id, true, secondAuthor, false, _now);
        var commissionClears = _competitions.SetCompetingFlag(second.Id, false, null, true, _now);

        await Assert.That(byStranger.StatusCode).IsEqualTo(403);
        await Assert.That(commissionSets.StatusCode).IsEqualTo(403);
        await Assert.That(commissionClears.Succeeded).IsTrue();
        await Assert.That(_motionRepo.Get(second.Id)!.IsCompetingMotion).IsFalse();
    }

    [Test]
    public async Task Flag_refused_once_editing_is_locked() {
        var (first, _) = Submitted();
        var (second, secondAuthor) = Submitted();
        _competitions.Declare(second.Id, first.Id, _userId, secondAuthor, false, _now);

        var outcome = _competitions.SetCompetingFlag(second.Id, true, secondAuthor, false, _now.AddDays(8));

        await Assert.That(outcome.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.EditLocked);
    }

    [Test]
    public async Task Removing_last_link_clears_flag() {
        var (first, _) = Submitted();
        var (third, _) = Submitted();
        var (second, secondAuthor) = Submitted();
        _competitions.Declare(second.Id, first.Id, _userId, secondAuthor, false, _now);
        _competitions.Declare(second.Id, third.Id, _userId, secondAuthor, false, _now);
        _competitions.SetCompetingFlag(second.Id, true, secondAuthor, false, _now);
        var before = _motionRepo.Get(second.Id)!;
        var toFirst = _competitionRepo.ListForMotion(second.Id).Single(l => l.MotionAId == first.Id || l.MotionBId == first.Id);
        var toThird = _competitionRepo.ListForMotion(second.Id).Single(l => l.MotionAId == third.Id || l.MotionBId == third.Id);

        _competitions.Remove(toFirst.Id, _userId, secondAuthor, false, _now);
        var stillFlagged = _motionRepo.Get(second.Id)!.IsCompetingMotion;
        _competitions.Remove(toThird.Id, _userId, secondAuthor, false, _now);
        var after = _motionRepo.Get(second.Id)!;

        await Assert.That(stillFlagged).IsTrue();
        await Assert.That(after.IsCompetingMotion).IsFalse();
        await Assert.That(after.Status).IsEqualTo(before.Status);
        await Assert.That(after.Revision).IsEqualTo(before.Revision);
        await Assert.That(after.SubmittedAt).IsEqualTo(before.SubmittedAt);
    }

    [Test]
    public async Task Declare_and_remove_queue_wiki_sync_for_numbered_motions() {
        var (first, firstAuthor) = Submitted();
        var (second, _) = Submitted();
        Db.AssemblyMotions
            .Where(m => m.Id == first.Id || m.Id == second.Id)
            .Set(m => m.WikiSyncState, WikiSyncState.Synced)
            .Update();

        _competitions.Declare(first.Id, second.Id, _userId, firstAuthor, false, _now);
        var afterDeclare = new[] { _motionRepo.Get(first.Id)!.WikiSyncState, _motionRepo.Get(second.Id)!.WikiSyncState };
        Db.AssemblyMotions
            .Where(m => m.Id == first.Id || m.Id == second.Id)
            .Set(m => m.WikiSyncState, WikiSyncState.Synced)
            .Update();
        _competitions.Remove(OnlyLink(first.Id).Id, _userId, firstAuthor, false, _now);

        await Assert.That(afterDeclare.All(s => s == WikiSyncState.Pending)).IsTrue();
        await Assert.That(_motionRepo.Get(first.Id)!.WikiSyncState).IsEqualTo(WikiSyncState.Pending);
        await Assert.That(_motionRepo.Get(second.Id)!.WikiSyncState).IsEqualTo(WikiSyncState.Pending);
    }

    [Test]
    public async Task Declare_on_submitted_motion_notifies_other_submitters() {
        var (first, firstAuthor) = Submitted();
        var (second, secondAuthor) = Submitted();
        _queue.Requests.Clear();

        _competitions.Declare(first.Id, second.Id, _userId, firstAuthor, false, _now);

        var request = _queue.Requests.Single();
        await Assert.That(request.TriggerId).IsEqualTo(NotificationTriggers.AssemblyMotionCompetitionDeclared);
        await Assert.That(PayloadOf(request).MemberIds.Single()).IsEqualTo(secondAuthor.Id);
    }

    [Test]
    public async Task Draft_declaration_notifies_on_release() {
        var (submitted, submittedAuthor) = Submitted();
        var (draft, draftAuthor) = NewMotion(false);
        _queue.Requests.Clear();

        var declared = _competitions.Declare(draft.Id, submitted.Id, _userId, draftAuthor, false, _now);
        var duringDraft = _queue.Requests.Count;
        _service.Release(draft.Id, draftAuthor, _now);

        var request = _queue.Requests.Single(r => r.TriggerId == NotificationTriggers.AssemblyMotionCompetitionDeclared);
        await Assert.That(declared.Succeeded).IsTrue();
        await Assert.That(duringDraft).IsEqualTo(0);
        await Assert.That(PayloadOf(request).MemberIds.Single()).IsEqualTo(submittedAuthor.Id);
    }

    [Test]
    public async Task Draft_declaration_only_by_its_author() {
        var (submitted, _) = Submitted();
        var (draft, _) = NewMotion(false);
        var stranger = _builder.SeedMember(chapterId: _chapter.Id);

        var byStranger = _competitions.Declare(draft.Id, submitted.Id, _userId, stranger, false, _now);
        var byCommission = _competitions.Declare(draft.Id, submitted.Id, _userId, null, true, _now);

        await Assert.That(byStranger.StatusCode).IsEqualTo(403);
        await Assert.That(byCommission.StatusCode).IsEqualTo(403);
    }

    [Test]
    public async Task Declare_against_foreign_draft_looks_like_missing() {
        var (first, author) = Submitted();
        var (draft, _) = NewMotion(false);

        var foreignDraft = _competitions.Declare(first.Id, draft.Id, _userId, author, false, _now);
        var missing = _competitions.Declare(first.Id, Guid.NewGuid(), _userId, author, false, _now);

        await Assert.That(foreignDraft.StatusCode).IsEqualTo(missing.StatusCode);
        await Assert.That(foreignDraft.ErrorKey).IsEqualTo(missing.ErrorKey);
        await Assert.That(missing.StatusCode).IsEqualTo(404);
    }

    [Test]
    public async Task Flag_refused_when_only_link_is_withdrawn() {
        var (first, firstAuthor) = Submitted();
        var (second, secondAuthor) = Submitted();
        _competitions.Declare(second.Id, first.Id, _userId, secondAuthor, false, _now);
        _service.Withdraw(first.Id, firstAuthor, _now);

        var outcome = _competitions.SetCompetingFlag(second.Id, true, secondAuthor, false, _now);

        await Assert.That(outcome.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.CompetingFlagRequiresLink);
        await Assert.That(_motionRepo.Get(second.Id)!.IsCompetingMotion).IsFalse();
    }

    [Test]
    public async Task Flag_stays_when_linked_motion_is_withdrawn_later() {
        var (first, firstAuthor) = Submitted();
        var (second, secondAuthor) = Submitted();
        _competitions.Declare(second.Id, first.Id, _userId, secondAuthor, false, _now);
        _competitions.SetCompetingFlag(second.Id, true, secondAuthor, false, _now);

        _service.Withdraw(first.Id, firstAuthor, _now);

        await Assert.That(_motionRepo.Get(second.Id)!.IsCompetingMotion).IsTrue();
    }

    [Test]
    public async Task Flag_authorization_runs_before_unchanged_shortcut() {
        var (first, _) = Submitted();
        var stranger = _builder.SeedMember(chapterId: _chapter.Id);

        var outcome = _competitions.SetCompetingFlag(first.Id, false, stranger, false, _now);

        await Assert.That(outcome.StatusCode).IsEqualTo(403);
    }

    [Test]
    public async Task Commission_member_acting_as_own_submitter_declares_as_submitter() {
        var (first, firstAuthor) = Submitted();
        var (second, _) = Submitted();

        _competitions.Declare(first.Id, second.Id, _userId, firstAuthor, true, _now);

        await Assert.That(OnlyLink(first.Id).DeclaredByCommission).IsFalse();
    }

    [Test]
    public async Task Release_notifies_once_and_not_again_after_later_edit() {
        var (submitted, _) = Submitted();
        var (draft, draftAuthor) = NewMotion(false);
        _competitions.Declare(draft.Id, submitted.Id, _userId, draftAuthor, false, _now);
        _queue.Requests.Clear();

        _service.Release(draft.Id, draftAuthor, _now);
        var secondRelease = _service.Release(draft.Id, draftAuthor, _now);
        _service.UpdateByAuthor(_motionRepo.Get(draft.Id)!, draftAuthor, _userId,
            new AssemblyMotionDraft(draft.TypeId, "Neu", null, "Neuer Text", "Grund", null, null, null), _motionRepo.Get(draft.Id)!.Revision, _now);

        await Assert.That(secondRelease.Succeeded).IsFalse();
        await Assert.That(_queue.Requests.Count(r => r.TriggerId == NotificationTriggers.AssemblyMotionCompetitionDeclared)).IsEqualTo(1);
    }

    private void SetDeadlines(DateTime deadline, DateTime competingDeadline)
        => Db.AssemblyMotionTypes
            .Where(t => t.PortalId == _portal.Id)
            .Set(t => t.Deadline, deadline)
            .Set(t => t.CompetingDeadline, competingDeadline)
            .Update();

    private AssemblyMotionDraft DraftOf(AssemblyMotion motion, string title)
        => new(motion.TypeId, title, null, "Antragstext", "Begründung", null, null, null);

    [Test]
    [Arguments(0.5, true)]
    [Arguments(2.0, true)]
    [Arguments(4.0, false)]
    public async Task Competing_motion_can_flag_edit_and_release_until_the_competing_deadline(double daysAfterStart, bool allowed) {
        var start = _now;
        SetDeadlines(start.AddDays(1), start.AddDays(3));
        var (first, _) = Submitted();
        var (draft, draftAuthor) = NewMotion(false);
        var at = start.AddDays(daysAfterStart);
        _competitions.Declare(draft.Id, first.Id, _userId, draftAuthor, false, at);

        var flag = _competitions.SetCompetingFlag(draft.Id, true, draftAuthor, false, at);
        var edit = _service.UpdateByAuthor(_motionRepo.Get(draft.Id)!, draftAuthor, _userId, DraftOf(draft, "Spät"), 1, at);
        var release = _service.Release(draft.Id, draftAuthor, at);

        await Assert.That(flag.Succeeded).IsEqualTo(allowed);
        await Assert.That(edit.Succeeded).IsEqualTo(allowed);
        await Assert.That(release.Succeeded).IsEqualTo(allowed);
        await Assert.That(_motionRepo.Get(draft.Id)!.Status).IsEqualTo(allowed ? AssemblyMotionStatus.Submitted : AssemblyMotionStatus.Draft);
        if (!allowed)
            await Assert.That(flag.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.EditLocked);
    }

    [Test]
    public async Task Motion_without_a_link_cannot_use_the_competing_deadline() {
        var start = _now;
        SetDeadlines(start.AddDays(1), start.AddDays(3));
        var (draft, draftAuthor) = NewMotion(false);
        var at = start.AddDays(2);

        var flag = _competitions.SetCompetingFlag(draft.Id, true, draftAuthor, false, at);
        var edit = _service.UpdateByAuthor(_motionRepo.Get(draft.Id)!, draftAuthor, _userId, DraftOf(draft, "Spät"), 1, at);
        var release = _service.Release(draft.Id, draftAuthor, at);

        await Assert.That(flag.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.CompetingFlagRequiresLink);
        await Assert.That(edit.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.EditLocked);
        await Assert.That(release.ErrorKey).StartsWith(I18nKey.Error.AssemblyMotion.DeadlinePassed);
        await Assert.That(_motionRepo.Get(draft.Id)!.IsCompetingMotion).IsFalse();
    }

    [Test]
    public async Task Removing_the_link_after_the_normal_deadline_takes_the_later_deadline_away() {
        var start = _now;
        SetDeadlines(start.AddDays(1), start.AddDays(3));
        var (first, _) = Submitted();
        var (draft, draftAuthor) = NewMotion(false);
        var at = start.AddDays(2);
        _competitions.Declare(draft.Id, first.Id, _userId, draftAuthor, false, at);
        _competitions.SetCompetingFlag(draft.Id, true, draftAuthor, false, at);

        _competitions.Remove(OnlyLink(draft.Id).Id, _userId, draftAuthor, false, at);
        var release = _service.Release(draft.Id, draftAuthor, at);

        await Assert.That(_motionRepo.Get(draft.Id)!.IsCompetingMotion).IsFalse();
        await Assert.That(release.ErrorKey).StartsWith(I18nKey.Error.AssemblyMotion.DeadlinePassed);
        await Assert.That(_motionRepo.Get(draft.Id)!.Status).IsEqualTo(AssemblyMotionStatus.Draft);
    }

    [Test]
    public async Task Clearing_the_flag_after_the_normal_deadline_lapses_a_collecting_motion() {
        var start = _now;
        SetDeadlines(start.AddDays(1), start.AddDays(3));
        var (first, _) = Submitted();
        var (collecting, collectingAuthor) = NewMotion(false, minSubmitters: 5);
        var at = start.AddDays(2);
        _competitions.Declare(collecting.Id, first.Id, _userId, collectingAuthor, false, at);
        _competitions.SetCompetingFlag(collecting.Id, true, collectingAuthor, false, at);
        _service.Release(collecting.Id, collectingAuthor, at);

        _competitions.SetCompetingFlag(collecting.Id, false, null, true, at);
        var lapsed = _service.LapseOverdue(at);

        await Assert.That(lapsed).IsEqualTo(1);
        await Assert.That(_motionRepo.Get(collecting.Id)!.Status).IsEqualTo(AssemblyMotionStatus.Lapsed);
    }

    [Test]
    public async Task Removing_a_link_keeps_the_wiki_requeue_when_the_flag_is_cleared() {
        var (first, _) = Submitted();
        var (second, secondAuthor) = Submitted();
        _competitions.Declare(second.Id, first.Id, _userId, secondAuthor, false, _now);
        _competitions.SetCompetingFlag(second.Id, true, secondAuthor, false, _now);
        Db.AssemblyMotions
            .Where(m => m.Id == first.Id || m.Id == second.Id)
            .Set(m => m.WikiSyncState, WikiSyncState.Synced)
            .Set(m => m.WikiSyncRequestedAt, (DateTime?)null)
            .Update();

        _competitions.Remove(OnlyLink(second.Id).Id, _userId, secondAuthor, false, _now);

        var reloaded = _motionRepo.Get(second.Id)!;
        await Assert.That(reloaded.IsCompetingMotion).IsFalse();
        await Assert.That(reloaded.WikiSyncState).IsEqualTo(WikiSyncState.Pending);
        await Assert.That(reloaded.WikiSyncRequestedAt).IsNotNull();
        await Assert.That(_motionRepo.Get(first.Id)!.WikiSyncState).IsEqualTo(WikiSyncState.Pending);
    }

    [Test]
    public async Task Release_of_a_linked_draft_queues_the_older_motion_for_the_wiki() {
        var (first, _) = Submitted();
        var (draft, draftAuthor) = NewMotion(false);
        _competitions.Declare(draft.Id, first.Id, _userId, draftAuthor, false, _now);
        MarkSynced(first.Id);

        _service.Release(draft.Id, draftAuthor, _now);

        await Assert.That(_motionRepo.Get(first.Id)!.WikiSyncState).IsEqualTo(WikiSyncState.Pending);
    }

    [Test]
    public async Task Final_join_of_a_linked_collecting_motion_queues_the_older_motion_for_the_wiki() {
        var (first, _) = Submitted();
        var (collecting, collectingAuthor) = NewMotion(false, minSubmitters: 5);
        _competitions.Declare(collecting.Id, first.Id, _userId, collectingAuthor, false, _now);
        _service.Release(collecting.Id, collectingAuthor, _now);
        MarkSynced(first.Id);
        for (var i = 0; i < 3; i++)
            _service.Join(collecting.Id, _builder.SeedMember(chapterId: _chapter.Id), _now);
        var beforeFinalJoin = _motionRepo.Get(first.Id)!.WikiSyncState;

        _service.Join(collecting.Id, _builder.SeedMember(chapterId: _chapter.Id), _now);

        await Assert.That(beforeFinalJoin).IsEqualTo(WikiSyncState.Synced);
        await Assert.That(_motionRepo.Get(collecting.Id)!.Number).IsNotNull();
        await Assert.That(_motionRepo.Get(first.Id)!.WikiSyncState).IsEqualTo(WikiSyncState.Pending);
    }

    [Test]
    public async Task Edit_of_a_numbered_motion_does_not_requeue_linked_motions() {
        var (first, _) = Submitted();
        var (second, secondAuthor) = Submitted();
        _competitions.Declare(second.Id, first.Id, _userId, secondAuthor, false, _now);
        MarkSynced(first.Id);

        _service.UpdateByAuthor(_motionRepo.Get(second.Id)!, secondAuthor, _userId, DraftOf(second, "Neu"), 1, _now);

        await Assert.That(_motionRepo.Get(first.Id)!.WikiSyncState).IsEqualTo(WikiSyncState.Synced);
    }

    private void MarkSynced(Guid motionId)
        => Db.AssemblyMotions
            .Where(m => m.Id == motionId)
            .Set(m => m.WikiSyncState, WikiSyncState.Synced)
            .Update();
}
