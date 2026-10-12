using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LinqToDB;
using Microsoft.Extensions.Logging.Abstractions;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Events;
using Quartermaster.Server.AssemblyMotions;
using Quartermaster.Server.AssemblyMotions.Notifications;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.AssemblyMotions;

public class AssemblyMotionResultServiceTests : RepositoryTestBase {
    private readonly Guid _userId = Guid.NewGuid();
    private DateTime _now = DateTime.UtcNow;
    private TestDataBuilder _builder = default!;
    private AssemblyMotionPortalRepository _portalRepo = default!;
    private AssemblyMotionRepository _motionRepo = default!;
    private AssemblyMotionService _service = default!;
    private AssemblyMotionResultService _results = default!;
    private Chapter _chapter = default!;
    private AssemblyMotionPortal _portal = default!;

    [Before(Test)]
    public void Setup() {
        _builder = new TestDataBuilder(Db);
        _portalRepo = new AssemblyMotionPortalRepository(Db, AuditLog);
        _motionRepo = new AssemblyMotionRepository(Db, AuditLog);
        _service = new AssemblyMotionService(_motionRepo, _portalRepo, AuditLog, Db,
            new AssemblyMotionNotifier(new CapturingNotificationQueue(), Db, NullLogger<AssemblyMotionNotifier>.Instance));
        _results = new AssemblyMotionResultService(_motionRepo, _portalRepo, new EventRepository(Db, AuditLog), AuditLog);
        _chapter = _builder.SeedChapter();
        SeedPortal(BerlinTime.FromUtc(_now).Date);
    }

    private void SeedPortal(DateTime? eventDate) {
        var ev = _builder.SeedEvent(_chapter.Id, eventDate: eventDate);
        _portal = _portalRepo.Create(ev.Id, _now.AddDays(-5), _now.AddDays(7), _now);
    }

    private AssemblyMotion NewMotion(bool release = true) {
        var type = _portalRepo.GetTypes(_portal.Id).First(t => t.MinSubmitters == 1);
        var author = _builder.SeedMember(chapterId: _chapter.Id);
        var draft = new AssemblyMotionDraft(type.Id, "Testantrag", null, "Antragstext", "Begründung", null, null, null);
        var motion = _service.CreateDraft(_portal, author, _userId, draft, _now).Motion!;
        return release ? _service.Release(motion.Id, author, _now).Motion! : motion;
    }

    private void ResetWikiState(Guid motionId)
        => Db.AssemblyMotions
            .Where(m => m.Id == motionId)
            .Set(m => m.WikiSyncState, WikiSyncState.Synced)
            .Update();

    private int ResultAuditCount(Guid motionId)
        => Db.AuditLogs.Count(a => a.EntityId == motionId && a.FieldName == nameof(AssemblyMotion.Result));

    [Test]
    public async Task Result_before_event_date_refused() {
        SeedPortal(BerlinTime.FromUtc(_now).Date.AddDays(1));
        var motion = NewMotion();

        var outcome = _results.SetResult(motion.Id, AssemblyMotionResult.Accepted, _now);

        await Assert.That(outcome.Succeeded).IsFalse();
        await Assert.That(outcome.StatusCode).IsEqualTo(409);
        await Assert.That(outcome.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.EventNotReached);
        await Assert.That(_motionRepo.Get(motion.Id)!.Result).IsEqualTo(AssemblyMotionResult.None);
    }

    [Test]
    public async Task Result_without_event_date_refused() {
        SeedPortal(null);
        var motion = NewMotion();

        var outcome = _results.SetResult(motion.Id, AssemblyMotionResult.Accepted, _now);

        await Assert.That(outcome.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.EventNotReached);
    }

    [Test]
    public async Task Result_on_event_day_allowed() {
        var motion = NewMotion();
        ResetWikiState(motion.Id);

        var outcome = _results.SetResult(motion.Id, AssemblyMotionResult.AcceptedModified, _now);
        var stored = _motionRepo.Get(motion.Id)!;

        await Assert.That(outcome.Succeeded).IsTrue();
        await Assert.That(stored.Result).IsEqualTo(AssemblyMotionResult.AcceptedModified);
        await Assert.That(stored.WikiSyncState).IsEqualTo(WikiSyncState.Pending);
        await Assert.That(ResultAuditCount(motion.Id)).IsEqualTo(1);
    }

    [Test]
    public async Task Result_allowed_after_event_day_and_on_withdrawn_motion() {
        var motion = NewMotion();
        _service.Withdraw(motion.Id, Db.Members.First(m => m.Id == motion.CreatedByMemberId), _now);

        var outcome = _results.SetResult(motion.Id, AssemblyMotionResult.NotHandled, _now.AddDays(3));

        await Assert.That(outcome.Succeeded).IsTrue();
        await Assert.That(_motionRepo.Get(motion.Id)!.Result).IsEqualTo(AssemblyMotionResult.NotHandled);
    }

    [Test]
    public async Task Result_requires_number() {
        var draft = NewMotion(release: false);

        var outcome = _results.SetResult(draft.Id, AssemblyMotionResult.Accepted, _now);

        await Assert.That(outcome.Succeeded).IsFalse();
        await Assert.That(outcome.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.InvalidState);
    }

    [Test]
    public async Task Unchanged_result_writes_no_audit_and_no_wiki_mark() {
        var motion = NewMotion();
        _results.SetResult(motion.Id, AssemblyMotionResult.Rejected, _now);
        ResetWikiState(motion.Id);

        var outcome = _results.SetResult(motion.Id, AssemblyMotionResult.Rejected, _now);

        await Assert.That(outcome.Succeeded).IsTrue();
        await Assert.That(ResultAuditCount(motion.Id)).IsEqualTo(1);
        await Assert.That(_motionRepo.Get(motion.Id)!.WikiSyncState).IsEqualTo(WikiSyncState.Synced);
    }

    [Test]
    public async Task None_clears_result() {
        var motion = NewMotion();
        _results.SetResult(motion.Id, AssemblyMotionResult.Accepted, _now);

        var outcome = _results.SetResult(motion.Id, AssemblyMotionResult.None, _now);

        await Assert.That(outcome.Succeeded).IsTrue();
        await Assert.That(_motionRepo.Get(motion.Id)!.Result).IsEqualTo(AssemblyMotionResult.None);
        await Assert.That(ResultAuditCount(motion.Id)).IsEqualTo(2);
    }

    [Test]
    public async Task Bulk_sets_all_results() {
        var first = NewMotion();
        var second = NewMotion();

        var outcome = _results.SetResults(_portal.Id, [
            new AssemblyMotionResultItem { MotionId = first.Id, Result = AssemblyMotionResult.Accepted },
            new AssemblyMotionResultItem { MotionId = second.Id, Result = AssemblyMotionResult.Rejected }
        ], _now);

        await Assert.That(outcome.Succeeded).IsTrue();
        await Assert.That(_motionRepo.Get(first.Id)!.Result).IsEqualTo(AssemblyMotionResult.Accepted);
        await Assert.That(_motionRepo.Get(second.Id)!.Result).IsEqualTo(AssemblyMotionResult.Rejected);
    }

    [Test]
    public async Task Bulk_is_atomic() {
        var valid = NewMotion();
        var unnumbered = NewMotion(release: false);

        var outcome = _results.SetResults(_portal.Id, [
            new AssemblyMotionResultItem { MotionId = valid.Id, Result = AssemblyMotionResult.Accepted },
            new AssemblyMotionResultItem { MotionId = unnumbered.Id, Result = AssemblyMotionResult.Accepted }
        ], _now);

        await Assert.That(outcome.Succeeded).IsFalse();
        await Assert.That(outcome.ErrorKey!).StartsWith(I18nKey.Error.AssemblyMotion.ResultItemInvalid);
        await Assert.That(_motionRepo.Get(valid.Id)!.Result).IsEqualTo(AssemblyMotionResult.None);
        await Assert.That(ResultAuditCount(valid.Id)).IsEqualTo(0);
    }

    [Test]
    public async Task Bulk_before_event_date_refused() {
        SeedPortal(BerlinTime.FromUtc(_now).Date.AddDays(1));
        var motion = NewMotion();

        var outcome = _results.SetResults(_portal.Id, [
            new AssemblyMotionResultItem { MotionId = motion.Id, Result = AssemblyMotionResult.Accepted }
        ], _now);

        await Assert.That(outcome.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.EventNotReached);
    }

    [Test]
    public async Task Bulk_rejects_motion_of_other_portal() {
        var own = NewMotion();
        var ownPortal = _portal;
        SeedPortal(BerlinTime.FromUtc(_now).Date);
        var foreign = NewMotion();

        var outcome = _results.SetResults(ownPortal.Id, [
            new AssemblyMotionResultItem { MotionId = own.Id, Result = AssemblyMotionResult.Accepted },
            new AssemblyMotionResultItem { MotionId = foreign.Id, Result = AssemblyMotionResult.Accepted }
        ], _now);

        await Assert.That(outcome.StatusCode).IsEqualTo(404);
        await Assert.That(_motionRepo.Get(own.Id)!.Result).IsEqualTo(AssemblyMotionResult.None);
        await Assert.That(_motionRepo.Get(foreign.Id)!.Result).IsEqualTo(AssemblyMotionResult.None);
    }

    [Test]
    [Arguments(2026, 7, 15, 22)]
    [Arguments(2026, 12, 15, 23)]
    public async Task Event_day_starts_at_berlin_midnight(int year, int month, int day, int utcHourOfBerlinMidnight) {
        var eventDate = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Unspecified);
        var berlinMidnightUtc = new DateTime(year, month, day, utcHourOfBerlinMidnight, 0, 0, DateTimeKind.Utc).AddDays(-1);

        var oneMinuteBefore = AssemblyMotionResultService.IsEventReached(eventDate, berlinMidnightUtc.AddMinutes(-1));
        var atMidnight = AssemblyMotionResultService.IsEventReached(eventDate, berlinMidnightUtc);

        await Assert.That(oneMinuteBefore).IsFalse();
        await Assert.That(atMidnight).IsTrue();
    }
}
