using LinqToDB;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.Forwarding;
using Quartermaster.Api.Motions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.Forwarding;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Motions;
using Quartermaster.Data.Notifications;
using Quartermaster.Data.Options;
using Quartermaster.Data.Templates;
using Quartermaster.Server.Email;
using Quartermaster.Server.Forwarding;
using Quartermaster.Server.Forwarding.Redmine;
using Quartermaster.Server.Messaging;
using Quartermaster.Server.Tests.Forwarding.Fakes;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Forwarding;

[NotInParallel("FakeRedmineClient")]
public class ForwardingJobTests : RepositoryTestBase {
    private const string FederalUrl = "https://redmine.bund.invalid";
    private const string StateUrl = "https://redmine.land.invalid";

    private readonly DateTime _now = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
    private TestDataBuilder _builder = default!;
    private ForwardingCaseRepository _cases = default!;
    private ForwardingSettingsRepository _settings = default!;
    private RedmineConnectionRepository _connections = default!;
    private MotionRepository _motions = default!;
    private Channel<EmailMessage> _mails = default!;
    private ForwardingJob _job = default!;
    private ForwardingCaseActions _actions = default!;
    private Chapter _federal = default!;
    private Chapter _state = default!;

    [Before(Test)]
    public void Setup() {
        FakeRedmineClient.Reset();
        _builder = new TestDataBuilder(Db);
        _cases = new ForwardingCaseRepository(Db);
        _settings = new ForwardingSettingsRepository(Db);
        _connections = new RedmineConnectionRepository(Db);
        _motions = new MotionRepository(Db, AuditLog);
        var chapters = new ChapterRepository(Db, AuditLog);
        var templates = new TemplateRepository(Db);
        templates.SupplementDefaults();
        var options = new OptionRepository(Db, AuditLog);
        options.SupplementDefaults();
        options.SetValue("system.public_base_url", null, "https://qm.test");
        var resolver = new ForwardingSettingsResolver(chapters, _settings, _connections);
        var renderer = new ForwardingTicketRenderer(templates, chapters, options);
        _mails = Channel.CreateUnbounded<EmailMessage>();
        var mailer = new ForwardingFailureMailer(new EmailMessageChannel(new NotificationLogRepository(Db), _mails), renderer, resolver, NullLogger<ForwardingFailureMailer>.Instance);
        _job = new ForwardingJob(_cases, _settings, resolver, renderer, new FakeRedmineClient(), _motions, new DueSelectionRepository(Db, AuditLog), new MembershipApplicationRepository(Db, AuditLog), mailer, NullLogger<ForwardingJob>.Instance);
        _actions = new ForwardingCaseActions(_cases);

        _federal = _builder.SeedChapter("Bund " + Guid.NewGuid().ToString("N")[..6]);
        _state = _builder.SeedChapter("Land", parentChapterId: _federal.Id);
        _connections.Upsert(_federal.Id, FederalUrl, "key-bund", _now);
        _connections.Upsert(_state.Id, StateUrl, "key-land", _now);
        _settings.SaveChapter(new ForwardingChapterSetting { ChapterId = _federal.Id, FailureEmail = "mv@bund.invalid" }, [
            new ForwardingTarget { Purpose = ForwardingPurpose.Reduction, ProjectId = 2, TrackerId = 3, NoteStatusId = 5, NoteAssigneeId = 77 },
            new ForwardingTarget { Purpose = ForwardingPurpose.Regular, ProjectId = 1, TrackerId = 3 }
        ]);
        _settings.SaveChapter(new ForwardingChapterSetting { ChapterId = _state.Id, ReductionMode = ForwardingMode.Redmine }, [
            new ForwardingTarget { Purpose = ForwardingPurpose.Reduction, ProjectId = 9, TrackerId = 3 }
        ]);
    }

    private ForwardingCase SeedCase(bool withChapterTicket = true, Guid? motionId = null) {
        var dues = new DueSelectionDTO { FirstName = "Anna", LastName = "Pirat", MemberNumber = 4711, SelectedValuation = SelectedValuation.Reduced, ReducedAmount = 36, ReducedJustification = "Studium" };
        var forwardingCase = new ForwardingCase {
            Kind = ForwardingCaseKind.DueSelection,
            DecidingChapterId = _state.Id,
            StateChapterId = _state.Id,
            MotionId = motionId,
            PayloadJson = JsonSerializer.Serialize(new ForwardingPayload(null, dues, null, "anna", false, _now)),
            CreatedAt = _now
        };
        var steps = new List<ForwardingStep> {
            new() { Order = 1, Kind = ForwardingStepKind.CreateTicket, TargetChapterId = _federal.Id, Purpose = ForwardingPurpose.Reduction, State = ForwardingStepState.Pending, UpdatedAt = _now }
        };
        if (withChapterTicket)
            steps.Add(new() { Order = 2, Kind = ForwardingStepKind.CreateTicket, TargetChapterId = _state.Id, Purpose = ForwardingPurpose.Reduction, State = ForwardingStepState.Pending, UpdatedAt = _now });
        if (motionId != null)
            steps.Add(new() { Order = steps.Count + 1, Kind = ForwardingStepKind.AddOutcomeNote, TargetChapterId = _federal.Id, Purpose = ForwardingPurpose.Reduction, State = ForwardingStepState.Waiting, UpdatedAt = _now });
        _cases.Create(forwardingCase, steps);
        return forwardingCase;
    }

    private List<EmailMessage> Mails() {
        var mails = new List<EmailMessage>();
        while (_mails.Reader.TryRead(out var mail))
            mails.Add(mail);
        return mails;
    }

    [Test]
    public async Task Federal_ticket_then_chapter_ticket_with_link() {
        var forwardingCase = SeedCase();

        await _job.RunAsync(_now, CancellationToken.None);

        var federal = FakeRedmineClient.Issues.Single(i => i.BaseUrl == FederalUrl);
        var state = FakeRedmineClient.Issues.Single(i => i.BaseUrl == StateUrl);
        var steps = _cases.GetSteps(forwardingCase.Id);
        await Assert.That(federal.ProjectId).IsEqualTo(2);
        await Assert.That(state.ProjectId).IsEqualTo(9);
        await Assert.That(state.Description).Contains($"{FederalUrl}/issues/{federal.Id}");
        await Assert.That(steps.All(s => s.State == ForwardingStepState.Done)).IsTrue();
        await Assert.That(steps[0].TicketUrl).IsEqualTo($"{FederalUrl}/issues/{federal.Id}");
        await Assert.That(_cases.Get(forwardingCase.Id)!.Status).IsEqualTo(ForwardingCaseStatus.Done);
    }

    [Test]
    public async Task Payload_cleared_when_tickets_done() {
        var forwardingCase = SeedCase();

        await _job.RunAsync(_now, CancellationToken.None);

        await Assert.That(_cases.Get(forwardingCase.Id)!.PayloadJson).IsNull();
    }

    [Test]
    public async Task Chapter_step_waits_for_federal_ticket() {
        var forwardingCase = SeedCase();
        FakeRedmineClient.ThrowOn["Create"] = new RedmineException("down", RedmineException.HttpError);

        await _job.RunAsync(_now, CancellationToken.None);

        var steps = _cases.GetSteps(forwardingCase.Id);
        await Assert.That(FakeRedmineClient.Count("Create")).IsEqualTo(1);
        await Assert.That(steps[1].Attempts).IsEqualTo(0);
        await Assert.That(steps[0].NextAttemptAt).IsEqualTo(_now.AddMinutes(5));
    }

    [Test]
    public async Task Retries_after_5_and_15_minutes_then_fails_and_mails() {
        var forwardingCase = SeedCase(withChapterTicket: false);
        FakeRedmineClient.ThrowOn["Create"] = new RedmineException("Redmine ist nicht erreichbar.", RedmineException.HttpError);

        await _job.RunAsync(_now, CancellationToken.None);
        var afterFirst = _cases.GetSteps(forwardingCase.Id)[0];
        await _job.RunAsync(_now.AddMinutes(5), CancellationToken.None);
        var afterSecond = _cases.GetSteps(forwardingCase.Id)[0];
        await _job.RunAsync(_now.AddMinutes(20), CancellationToken.None);
        var afterThird = _cases.GetSteps(forwardingCase.Id)[0];

        var mail = Mails().Single();
        await Assert.That(afterFirst.NextAttemptAt).IsEqualTo(_now.AddMinutes(5));
        await Assert.That(afterSecond.NextAttemptAt).IsEqualTo(_now.AddMinutes(20));
        await Assert.That(afterThird.State).IsEqualTo(ForwardingStepState.Failed);
        await Assert.That(_cases.Get(forwardingCase.Id)!.Status).IsEqualTo(ForwardingCaseStatus.Failed);
        await Assert.That(_cases.Get(forwardingCase.Id)!.PayloadJson).IsNotNull();
        await Assert.That(mail.To).IsEqualTo("mv@bund.invalid");
        await Assert.That(mail.Subject).Contains(forwardingCase.Reference);
        await Assert.That(mail.Body).Contains("Studium");
    }

    [Test]
    public async Task Rejected_fails_at_once() {
        var forwardingCase = SeedCase(withChapterTicket: false);
        FakeRedmineClient.ThrowOn["Create"] = new RedmineException("Redmine hat das Ticket abgelehnt: Tracker ungültig", RedmineException.Rejected, 422);

        await _job.RunAsync(_now, CancellationToken.None);

        var step = _cases.GetSteps(forwardingCase.Id)[0];
        await Assert.That(step.State).IsEqualTo(ForwardingStepState.Failed);
        await Assert.That(step.LastError).Contains("Tracker ungültig");
        await Assert.That(Mails().Count).IsEqualTo(1);
    }

    [Test]
    public async Task Missing_connection_fails_permanently() {
        _connections.Delete(_federal.Id);
        var forwardingCase = SeedCase(withChapterTicket: false);

        await _job.RunAsync(_now, CancellationToken.None);

        var step = _cases.GetSteps(forwardingCase.Id)[0];
        await Assert.That(step.State).IsEqualTo(ForwardingStepState.Failed);
        await Assert.That(step.LastError).Contains("Redmine-Verbindung");
    }

    [Test]
    public async Task In_flight_step_adopts_ticket_found_by_reference() {
        var forwardingCase = SeedCase(withChapterTicket: false);
        FakeRedmineClient.ThrowAfterCreate = new RedmineException("Zeitüberschreitung", RedmineException.Timeout);
        await _job.RunAsync(_now, CancellationToken.None);
        FakeRedmineClient.ThrowAfterCreate = null;

        await _job.RunAsync(_now.AddMinutes(5), CancellationToken.None);

        var step = _cases.GetSteps(forwardingCase.Id)[0];
        await Assert.That(FakeRedmineClient.Issues.Count).IsEqualTo(1);
        await Assert.That(step.State).IsEqualTo(ForwardingStepState.Done);
        await Assert.That(step.TicketId).IsEqualTo(FakeRedmineClient.Issues.Single().Id);
    }

    [Test]
    public async Task Crashed_in_flight_step_searches_before_creating() {
        var forwardingCase = SeedCase(withChapterTicket: false);
        await _job.RunAsync(_now, CancellationToken.None);
        var issue = FakeRedmineClient.Issues.Single();
        var step = _cases.GetSteps(forwardingCase.Id)[0];
        step.State = ForwardingStepState.InFlight;
        step.TicketId = null;
        _cases.UpdateStep(step);
        _cases.SetCaseStatus(forwardingCase.Id, ForwardingCaseStatus.Done, ForwardingCaseStatus.Open, null);

        await _job.RunAsync(_now.AddMinutes(1), CancellationToken.None);

        await Assert.That(FakeRedmineClient.Issues.Count).IsEqualTo(1);
        await Assert.That(_cases.GetSteps(forwardingCase.Id)[0].TicketId).IsEqualTo(issue.Id);
    }

    [Test]
    public async Task Note_waits_for_decision_then_updates_federal_ticket() {
        var motion = Motion.Create(_state.Id, "Anna", "a@example.org", "Beitragsminderung", "x", "<p>x</p>", _now, null, null);
        _motions.Create(motion);
        var forwardingCase = SeedCase(withChapterTicket: false, motionId: motion.Id);

        await _job.RunAsync(_now, CancellationToken.None);
        var waiting = _cases.GetSteps(forwardingCase.Id)[1].State;
        _motions.UpdateApprovalStatus(motion.Id, MotionApprovalStatus.Approved);
        new ForwardingOutcomeHook(_cases, _motions).OnMotionResolved(motion.Id, _now);
        await _job.RunAsync(_now.AddMinutes(1), CancellationToken.None);

        var issue = FakeRedmineClient.Issues.Single();
        await Assert.That(waiting).IsEqualTo(ForwardingStepState.Waiting);
        await Assert.That(issue.Notes.Single()).Contains("angenommen");
        await Assert.That(issue.StatusId).IsEqualTo(5);
        await Assert.That(issue.AssigneeId).IsEqualTo(77);
        await Assert.That(_cases.Get(forwardingCase.Id)!.Status).IsEqualTo(ForwardingCaseStatus.Done);
    }

    [Test]
    public async Task Retry_resets_failed_steps() {
        var forwardingCase = SeedCase(withChapterTicket: false);
        FakeRedmineClient.ThrowOn["Create"] = new RedmineException("abgelehnt", RedmineException.Rejected, 422);
        await _job.RunAsync(_now, CancellationToken.None);
        FakeRedmineClient.ThrowOn.Clear();

        var retried = _actions.Retry(forwardingCase.Id, _now);
        await _job.RunAsync(_now.AddMinutes(1), CancellationToken.None);

        await Assert.That(retried).IsTrue();
        await Assert.That(_cases.GetSteps(forwardingCase.Id)[0].State).IsEqualTo(ForwardingStepState.Done);
        await Assert.That(_cases.Get(forwardingCase.Id)!.Status).IsEqualTo(ForwardingCaseStatus.Done);
    }

    [Test]
    public async Task Resolve_clears_payload_and_skips_steps() {
        var forwardingCase = SeedCase();
        FakeRedmineClient.ThrowOn["Create"] = new RedmineException("abgelehnt", RedmineException.Rejected, 422);
        await _job.RunAsync(_now, CancellationToken.None);

        var resolved = _actions.Resolve(forwardingCase.Id, _now);

        var reloaded = _cases.Get(forwardingCase.Id)!;
        await Assert.That(resolved).IsTrue();
        await Assert.That(reloaded.Status).IsEqualTo(ForwardingCaseStatus.ManuallyResolved);
        await Assert.That(reloaded.PayloadJson).IsNull();
        await Assert.That(_cases.GetSteps(forwardingCase.Id).All(s => s.State == ForwardingStepState.Skipped)).IsTrue();
    }

    [Test]
    public async Task Failure_mail_goes_to_inherited_address() {
        _settings.SaveChapter(new ForwardingChapterSetting { ChapterId = _state.Id, ReductionMode = ForwardingMode.Redmine }, [
            new ForwardingTarget { Purpose = ForwardingPurpose.Reduction, ProjectId = 9, TrackerId = 3, CustomFieldsJson = "{broken" }
        ]);
        SeedCase();

        await _job.RunAsync(_now, CancellationToken.None);

        await Assert.That(Mails().Single().To).IsEqualTo("mv@bund.invalid");
    }
    [Test]
    public async Task Retry_after_timeout_adopts_ticket_redmine_created() {
        var forwardingCase = SeedCase(withChapterTicket: false);
        FakeRedmineClient.ThrowOn["Create"] = new RedmineException("Redmine ist nicht erreichbar.", RedmineException.HttpError);
        await _job.RunAsync(_now, CancellationToken.None);
        await _job.RunAsync(_now.AddMinutes(5), CancellationToken.None);
        FakeRedmineClient.ThrowOn.Clear();
        FakeRedmineClient.ThrowAfterCreate = new RedmineException("Zeitüberschreitung", RedmineException.Timeout);
        await _job.RunAsync(_now.AddMinutes(20), CancellationToken.None);
        FakeRedmineClient.ThrowAfterCreate = null;

        _actions.Retry(forwardingCase.Id, _now.AddMinutes(30));
        await _job.RunAsync(_now.AddMinutes(30), CancellationToken.None);

        await Assert.That(FakeRedmineClient.Issues.Count).IsEqualTo(1);
        await Assert.That(_cases.GetSteps(forwardingCase.Id)[0].TicketId).IsEqualTo(FakeRedmineClient.Issues.Single().Id);
    }
    [Test]
    public async Task Resolving_clears_the_failure_mail_body() {
        var forwardingCase = SeedCase(withChapterTicket: false);
        FakeRedmineClient.ThrowOn["Create"] = new RedmineException("abgelehnt", RedmineException.Rejected, 422);
        await _job.RunAsync(_now, CancellationToken.None);
        var before = Db.NotificationLogs.Single(l => l.SourceEntityId == forwardingCase.Id).Body;

        _actions.Resolve(forwardingCase.Id, _now);

        await Assert.That(before).Contains("Studium");
        await Assert.That(Db.NotificationLogs.Single(l => l.SourceEntityId == forwardingCase.Id).Body).IsNull();
    }
    [Test]
    public async Task Resolving_while_a_ticket_is_created_keeps_the_case_resolved() {
        var forwardingCase = SeedCase();
        FakeRedmineClient.BeforeCall = method => {
            if (method == "Create")
                _actions.Resolve(forwardingCase.Id, _now);
        };

        await _job.RunAsync(_now, CancellationToken.None);

        await Assert.That(_cases.Get(forwardingCase.Id)!.Status).IsEqualTo(ForwardingCaseStatus.ManuallyResolved);
        await Assert.That(FakeRedmineClient.Count("Create")).IsEqualTo(1);
    }

    [Test]
    public async Task Resolving_while_a_create_fails_sends_no_failure_mail() {
        var forwardingCase = SeedCase(withChapterTicket: false);
        FakeRedmineClient.ThrowOn["Create"] = new RedmineException("abgelehnt", RedmineException.Rejected, 422);
        FakeRedmineClient.BeforeCall = method => {
            if (method == "Create")
                _actions.Resolve(forwardingCase.Id, _now);
        };

        await _job.RunAsync(_now, CancellationToken.None);

        await Assert.That(_cases.Get(forwardingCase.Id)!.Status).IsEqualTo(ForwardingCaseStatus.ManuallyResolved);
        await Assert.That(Mails()).IsEmpty();
    }

    [Test]
    public async Task Step_write_with_an_expected_state_leaves_a_changed_step_alone() {
        var forwardingCase = SeedCase(withChapterTicket: false, motionId: Guid.NewGuid());
        _actions.Resolve(forwardingCase.Id, _now);
        var note = _cases.GetSteps(forwardingCase.Id).Single(s => s.Kind == ForwardingStepKind.AddOutcomeNote);
        note.State = ForwardingStepState.Pending;

        var written = _cases.UpdateStep(note, ForwardingStepState.Waiting);

        await Assert.That(written).IsFalse();
        await Assert.That(_cases.GetSteps(forwardingCase.Id).Single(s => s.Kind == ForwardingStepKind.AddOutcomeNote).State).IsEqualTo(ForwardingStepState.Skipped);
    }
    [Test]
    public async Task Case_waiting_for_a_retry_does_not_take_a_batch_slot() {
        var waiting = SeedCase();
        var waitingFirst = _cases.GetSteps(waiting.Id)[0];
        waitingFirst.Attempts = 1;
        waitingFirst.NextAttemptAt = _now.AddMinutes(5);
        _cases.UpdateStep(waitingFirst);
        Db.ForwardingCases.Where(c => c.Id == waiting.Id).Set(c => c.CreatedAt, _now.AddHours(-1)).Update();
        var fresh = SeedCase();

        var due = _cases.ListCasesWithDueSteps(_now, 1);

        await Assert.That(due).IsEquivalentTo(new[] { fresh.Id });
    }
    [Test]
    public async Task Note_after_a_direct_decision_uses_the_due_selection() {
        var dueSelections = new DueSelectionRepository(Db, AuditLog);
        var record = new DueSelection { FirstName = "Anna", LastName = "Pirat", SelectedValuation = SelectedValuation.Reduced, ReducedAmount = 36, ChapterId = _state.Id };
        dueSelections.Create(record);
        var motion = Motion.Create(_state.Id, "Anna", "a@example.org", "Beitragsminderung", "x", "<p>x</p>", _now, null, record.Id);
        _motions.Create(motion);
        var forwardingCase = SeedCase(withChapterTicket: false, motionId: motion.Id);
        Db.ForwardingCases.Where(c => c.Id == forwardingCase.Id).Set(c => c.DueSelectionId, (Guid?)record.Id).Update();
        await _job.RunAsync(_now, CancellationToken.None);

        dueSelections.UpdateStatus(record.Id, DueSelectionStatus.Rejected, null);
        new ForwardingOutcomeHook(_cases, _motions).OnRecordDecided(ForwardingCaseKind.DueSelection, record.Id, _now);
        await _job.RunAsync(_now.AddMinutes(1), CancellationToken.None);

        var issue = FakeRedmineClient.Issues.Single();
        await Assert.That(issue.Notes.Single()).Contains("abgelehnt");
        await Assert.That(issue.Notes.Single()).Contains($"/Administration/DueSelections/{record.Id}");
        await Assert.That(_cases.Get(forwardingCase.Id)!.Status).IsEqualTo(ForwardingCaseStatus.Done);
    }
}
