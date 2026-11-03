using LinqToDB;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.Forwarding;
using Quartermaster.Api.MembershipApplications;
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
using Quartermaster.Server.Forwarding.SageEntry;
using Quartermaster.Server.MembershipApplications;
using Quartermaster.Server.Messaging;
using Quartermaster.Server.Notifications;
using Quartermaster.Server.Tests.Forwarding.Fakes;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Forwarding;

[NotInParallel("FakeRedmineClient")]
public class ForwardingJobTests : RepositoryTestBase {
    private const string FederalUrl = "https://redmine.bund.invalid";
    private const string StateUrl = "https://redmine.land.invalid";
    private const string ApplicantEmail = "bea@example.org";

    private readonly DateTime _now = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
    private TestDataBuilder _builder = default!;
    private ForwardingCaseRepository _cases = default!;
    private ForwardingSettingsRepository _settings = default!;
    private RedmineConnectionRepository _connections = default!;
    private MotionRepository _motions = default!;
    private MembershipApplicationRepository _applications = default!;
    private DueSelectionRepository _dueSelections = default!;
    private OptionRepository _options = default!;
    private ForwardingOutcomeHook _hook = default!;
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
        _applications = new MembershipApplicationRepository(Db, AuditLog);
        _dueSelections = new DueSelectionRepository(Db, AuditLog);
        var chapters = new ChapterRepository(Db, AuditLog);
        var templates = new TemplateRepository(Db);
        templates.SupplementDefaults();
        _options = new OptionRepository(Db, AuditLog);
        _options.SupplementDefaults();
        _options.SetValue("system.public_base_url", null, "https://qm.test");
        _options.SetValue(ForwardingOptionKeys.MembershipApplicationEnabled, null, "true");
        var resolver = new ForwardingSettingsResolver(chapters, _settings, _connections);
        var renderer = new ForwardingTicketRenderer(templates, chapters, _options);
        var redmine = new FakeRedmineClient();
        _mails = Channel.CreateUnbounded<EmailMessage>();
        var channel = new EmailMessageChannel(new NotificationLogRepository(Db), _mails);
        var mailer = new ForwardingFailureMailer(channel, renderer, resolver, NullLogger<ForwardingFailureMailer>.Instance);
        var applicationMails = new MembershipApplicationMailService(templates, chapters, channel, new NotificationTemplateGlobals(_options), NullLogger<MembershipApplicationMailService>.Instance);
        var gate = new SageEntryGate(_cases, _applications, _dueSelections);
        _job = new ForwardingJob(_cases, _settings, resolver, renderer, redmine, _motions, _dueSelections, _applications, mailer, gate,
            new SageEntryNote(renderer, redmine, gate, _dueSelections), new SageEntryCheck(redmine), new SageEntrySetup(_options, chapters, _settings, resolver),
            applicationMails, NullLogger<ForwardingJob>.Instance);
        _actions = new ForwardingCaseActions(_cases);
        _hook = new ForwardingOutcomeHook(_cases, _motions, _applications);

        _federal = _builder.SeedChapter("Bund " + Guid.NewGuid().ToString("N")[..6]);
        _state = _builder.SeedChapter("Land", parentChapterId: _federal.Id);
        _connections.Upsert(_federal.Id, FederalUrl, "key-bund", _now);
        _connections.Upsert(_state.Id, StateUrl, "key-land", _now);
        SaveFederalTargets(ApplicationTarget());
        _settings.SaveChapter(new ForwardingChapterSetting { ChapterId = _state.Id, ReductionMode = ForwardingMode.Redmine }, [
            new ForwardingTarget { Purpose = ForwardingPurpose.Reduction, ProjectId = 9, TrackerId = 3 }
        ]);
    }

    private static ForwardingTarget ApplicationTarget(int? sageAssigneeId = 12, int? sageDoneStatusId = 5, bool awaitReassignment = false) {
        return new ForwardingTarget {
            Purpose = ForwardingPurpose.Application,
            ProjectId = 4,
            TrackerId = 3,
            NoteStatusId = 4,
            NoteAssigneeId = 77,
            SageAssigneeId = sageAssigneeId,
            SageDoneStatusId = sageDoneStatusId,
            SageAwaitReassignment = awaitReassignment
        };
    }

    private void SaveFederalTargets(ForwardingTarget applicationTarget) {
        _settings.SaveChapter(new ForwardingChapterSetting { ChapterId = _federal.Id, FailureEmail = "mv@bund.invalid" }, [
            new ForwardingTarget { Purpose = ForwardingPurpose.Reduction, ProjectId = 2, TrackerId = 3, NoteStatusId = 5, NoteAssigneeId = 77 },
            new ForwardingTarget { Purpose = ForwardingPurpose.Regular, ProjectId = 1, TrackerId = 3 },
            applicationTarget
        ]);
    }

    private MembershipApplication SeedApplication(SelectedValuation valuation = SelectedValuation.MonthlyPayGroup, DueSelectionStatus dueStatus = DueSelectionStatus.AutoApproved) {
        var dueRecord = new DueSelection {
            FirstName = "Bea",
            LastName = "Neu",
            ChapterId = _state.Id,
            SelectedValuation = valuation,
            SelectedDue = 120,
            ReducedAmount = 36,
            ReducedJustification = "Studium",
            Status = dueStatus
        };
        _dueSelections.Create(dueRecord);
        var application = new MembershipApplication {
            FirstName = "Bea",
            LastName = "Neu",
            Email = ApplicantEmail,
            ChapterId = _state.Id,
            DueSelectionId = dueRecord.Id,
            DateOfBirth = new DateTime(2000, 1, 1),
            AddressStreet = "Weg",
            AddressHouseNbr = "1",
            AddressPostCode = "30159",
            AddressCity = "Hannover",
            ConformityDeclarationAccepted = true,
            EntryDate = new DateTime(2026, 10, 1),
            SubmittedAt = _now,
            Status = ApplicationStatus.Pending
        };
        _applications.Create(application);
        return application;
    }

    private ForwardingCase SeedApplicationCase(MembershipApplication application, Guid? motionId = null, bool withSageStep = true) {
        var forwardingCase = new ForwardingCase {
            Kind = ForwardingCaseKind.Application,
            ApplicationChapterId = _state.Id,
            DecidingChapterId = _state.Id,
            StateChapterId = _state.Id,
            MembershipApplicationId = application.Id,
            MotionId = motionId,
            CreatedAt = _now
        };
        var steps = new List<ForwardingStep> {
            new() { Order = 1, Kind = ForwardingStepKind.CreateTicket, TargetChapterId = _federal.Id, Purpose = ForwardingPurpose.Application, State = ForwardingStepState.Pending, UpdatedAt = _now },
            new() { Order = 2, Kind = ForwardingStepKind.AddOutcomeNote, TargetChapterId = _federal.Id, Purpose = ForwardingPurpose.Application, State = ForwardingStepState.Waiting, UpdatedAt = _now }
        };
        if (withSageStep)
            steps.Add(new() { Order = 3, Kind = ForwardingStepKind.AwaitSageEntry, TargetChapterId = _federal.Id, Purpose = ForwardingPurpose.Application, State = ForwardingStepState.Waiting, UpdatedAt = _now });
        _cases.Create(forwardingCase, steps);
        return forwardingCase;
    }

    /// <summary>Creates the federal ticket, decides the application, releases its steps and runs the job a minute later.</summary>
    private async Task<FakeRedmineIssue> DecideAsync(MembershipApplication application, ApplicationStatus status) {
        await _job.RunAsync(_now, CancellationToken.None);
        _applications.UpdateStatus(application.Id, status, null);
        _hook.OnRecordDecided(ForwardingCaseKind.Application, application.Id, _now);
        await _job.RunAsync(_now.AddMinutes(1), CancellationToken.None);
        return FakeRedmineClient.Issues.Single();
    }

    private ForwardingStep StepOf(ForwardingCase forwardingCase, ForwardingStepKind kind) {
        return _cases.GetSteps(forwardingCase.Id).Single(s => s.Kind == kind);
    }

    private List<EmailMessage> ApprovalMails(List<EmailMessage> mails) {
        return mails.Where(m => m.To == ApplicantEmail).ToList();
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
        _motions.UpdatePendingApprovalStatus(motion.Id, MotionApprovalStatus.Approved);
        new ForwardingOutcomeHook(_cases, _motions, new MembershipApplicationRepository(Db, AuditLog)).OnMotionResolved(motion.Id, _now);
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
        new ForwardingOutcomeHook(_cases, _motions, new MembershipApplicationRepository(Db, AuditLog)).OnRecordDecided(ForwardingCaseKind.DueSelection, record.Id, _now);
        await _job.RunAsync(_now.AddMinutes(1), CancellationToken.None);

        var issue = FakeRedmineClient.Issues.Single();
        await Assert.That(issue.Notes.Single()).Contains("abgelehnt");
        await Assert.That(issue.Notes.Single()).Contains($"/Administration/DueSelections/{record.Id}");
        await Assert.That(_cases.Get(forwardingCase.Id)!.Status).IsEqualTo(ForwardingCaseStatus.Done);
    }

    [Test]
    public async Task Approval_note_adds_sage_request_and_assigns_group() {
        var application = SeedApplication();
        SeedApplicationCase(application);

        var issue = await DecideAsync(application, ApplicationStatus.Approved);

        var note = issue.Notes.Single();
        await Assert.That(note).Contains("angenommen");
        await Assert.That(note).Contains("\n\n**Bitte um Eintragung in SAGE**");
        await Assert.That(note).Contains($"Vorläufige Mitgliedsnummer: RM{issue.Id}");
        await Assert.That(note).Contains("Gewünschtes Eintrittsdatum: 01.10.2026");
        await Assert.That(note).Contains("Beitrag 2026 anteilig ab Oktober: 30,00 €");
        await Assert.That(note).Contains("Jahresbeitrag: 120,00 €");
        await Assert.That(note).Contains("„Zur Eintragung“");
        await Assert.That(issue.AssigneeId).IsEqualTo(12);
        await Assert.That(issue.StatusId).IsNull();
    }

    [Test]
    public async Task Rejection_note_uses_note_status_and_skips_sage_step() {
        var application = SeedApplication();
        var forwardingCase = SeedApplicationCase(application);

        var issue = await DecideAsync(application, ApplicationStatus.Rejected);

        await Assert.That(issue.Notes.Single()).Contains("abgelehnt");
        await Assert.That(issue.Notes.Single()).DoesNotContain("SAGE");
        await Assert.That(issue.StatusId).IsEqualTo(4);
        await Assert.That(issue.AssigneeId).IsEqualTo(77);
        await Assert.That(StepOf(forwardingCase, ForwardingStepKind.AwaitSageEntry).State).IsEqualTo(ForwardingStepState.Skipped);
        await Assert.That(_cases.Get(forwardingCase.Id)!.Status).IsEqualTo(ForwardingCaseStatus.Done);
        await Assert.That(FakeRedmineClient.Count("Issue")).IsEqualTo(0);
    }

    [Test]
    public async Task Waits_and_reschedules_without_counting_attempts() {
        var application = SeedApplication();
        var forwardingCase = SeedApplicationCase(application);

        await DecideAsync(application, ApplicationStatus.Approved);

        var step = StepOf(forwardingCase, ForwardingStepKind.AwaitSageEntry);
        await Assert.That(FakeRedmineClient.Count("Issue")).IsEqualTo(2);
        await Assert.That(step.State).IsEqualTo(ForwardingStepState.Pending);
        await Assert.That(step.NextAttemptAt).IsEqualTo(_now.AddMinutes(16));
        await Assert.That(step.Attempts).IsEqualTo(0);
        await Assert.That(step.LastError).IsNull();
        await Assert.That(_cases.Get(forwardingCase.Id)!.Status).IsEqualTo(ForwardingCaseStatus.Open);
        await Assert.That(ApprovalMails(Mails())).IsEmpty();
    }

    [Test]
    public async Task Status_signal_sends_mail() {
        var application = SeedApplication();
        var forwardingCase = SeedApplicationCase(application);
        var issue = await DecideAsync(application, ApplicationStatus.Approved);

        issue.StatusId = 5;
        await _job.RunAsync(_now.AddMinutes(16), CancellationToken.None);

        var mail = ApprovalMails(Mails()).Single();
        await Assert.That(mail.Subject).IsEqualTo("Dein Mitgliedsantrag wurde genehmigt");
        await Assert.That(StepOf(forwardingCase, ForwardingStepKind.AwaitSageEntry).State).IsEqualTo(ForwardingStepState.Done);
        await Assert.That(_cases.Get(forwardingCase.Id)!.Status).IsEqualTo(ForwardingCaseStatus.Done);
    }

    [Test]
    public async Task Reassignment_signal_sends_mail() {
        SaveFederalTargets(ApplicationTarget(sageDoneStatusId: null, awaitReassignment: true));
        var application = SeedApplication();
        var forwardingCase = SeedApplicationCase(application);
        var issue = await DecideAsync(application, ApplicationStatus.Approved);
        var waitedFirst = ApprovalMails(Mails()).Count;

        issue.AssigneeId = FakeRedmineClient.CurrentUserId;
        await _job.RunAsync(_now.AddMinutes(16), CancellationToken.None);

        await Assert.That(waitedFirst).IsEqualTo(0);
        await Assert.That(issue.Notes.Single()).Contains("dem Konto qm-bot zugewiesen ist");
        await Assert.That(ApprovalMails(Mails()).Count).IsEqualTo(1);
        await Assert.That(StepOf(forwardingCase, ForwardingStepKind.AwaitSageEntry).State).IsEqualTo(ForwardingStepState.Done);
        await Assert.That(_cases.Get(forwardingCase.Id)!.Status).IsEqualTo(ForwardingCaseStatus.Done);
    }

    [Test]
    public async Task Both_signals_needed_when_both_set() {
        SaveFederalTargets(ApplicationTarget(awaitReassignment: true));
        var application = SeedApplication();
        var forwardingCase = SeedApplicationCase(application);
        var issue = await DecideAsync(application, ApplicationStatus.Approved);

        issue.StatusId = 5;
        await _job.RunAsync(_now.AddMinutes(16), CancellationToken.None);
        var afterStatus = ApprovalMails(Mails()).Count;
        var stepAfterStatus = StepOf(forwardingCase, ForwardingStepKind.AwaitSageEntry);
        issue.AssigneeId = FakeRedmineClient.CurrentUserId;
        await _job.RunAsync(_now.AddMinutes(31), CancellationToken.None);

        await Assert.That(afterStatus).IsEqualTo(0);
        await Assert.That(stepAfterStatus.State).IsEqualTo(ForwardingStepState.Pending);
        await Assert.That(stepAfterStatus.NextAttemptAt).IsEqualTo(_now.AddMinutes(31));
        await Assert.That(ApprovalMails(Mails()).Count).IsEqualTo(1);
        await Assert.That(StepOf(forwardingCase, ForwardingStepKind.AwaitSageEntry).State).IsEqualTo(ForwardingStepState.Done);
    }

    [Test]
    public async Task Mail_sent_once_when_check_runs_twice() {
        var application = SeedApplication();
        SeedApplicationCase(application);
        var issue = await DecideAsync(application, ApplicationStatus.Approved);

        issue.StatusId = 5;
        await _job.RunAsync(_now.AddMinutes(16), CancellationToken.None);
        await _job.RunAsync(_now.AddMinutes(31), CancellationToken.None);

        await Assert.That(ApprovalMails(Mails()).Count).IsEqualTo(1);
        await Assert.That(FakeRedmineClient.Count("Issue")).IsEqualTo(3);
    }

    [Test]
    public async Task Check_error_retries_then_fails_and_keeps_mail_held() {
        var application = SeedApplication();
        var forwardingCase = SeedApplicationCase(application);
        var issue = await DecideAsync(application, ApplicationStatus.Approved);
        issue.StatusId = 5;
        FakeRedmineClient.ThrowOn["Issue"] = new RedmineException("Redmine ist nicht erreichbar.", RedmineException.HttpError);

        await _job.RunAsync(_now.AddMinutes(16), CancellationToken.None);
        var afterFirst = StepOf(forwardingCase, ForwardingStepKind.AwaitSageEntry);
        await _job.RunAsync(_now.AddMinutes(21), CancellationToken.None);
        var afterSecond = StepOf(forwardingCase, ForwardingStepKind.AwaitSageEntry);
        await _job.RunAsync(_now.AddMinutes(36), CancellationToken.None);
        var afterThird = StepOf(forwardingCase, ForwardingStepKind.AwaitSageEntry);

        var mails = Mails();
        await Assert.That(afterFirst.Attempts).IsEqualTo(1);
        await Assert.That(afterFirst.NextAttemptAt).IsEqualTo(_now.AddMinutes(21));
        await Assert.That(afterSecond.Attempts).IsEqualTo(2);
        await Assert.That(afterThird.State).IsEqualTo(ForwardingStepState.Failed);
        await Assert.That(afterThird.LastError).Contains("nicht erreichbar");
        await Assert.That(_cases.Get(forwardingCase.Id)!.Status).IsEqualTo(ForwardingCaseStatus.Failed);
        await Assert.That(mails.Single(m => m.To == "mv@bund.invalid").Body).Contains("auf die SAGE-Eintragung geprüft");
        await Assert.That(ApprovalMails(mails)).IsEmpty();
    }

    [Test]
    public async Task Missing_setup_fails_note_and_holds_mail() {
        SaveFederalTargets(ApplicationTarget(sageAssigneeId: null));
        var application = SeedApplication();
        var forwardingCase = SeedApplicationCase(application);

        var issue = await DecideAsync(application, ApplicationStatus.Approved);

        var note = StepOf(forwardingCase, ForwardingStepKind.AddOutcomeNote);
        var mails = Mails();
        await Assert.That(note.State).IsEqualTo(ForwardingStepState.Failed);
        await Assert.That(note.Attempts).IsEqualTo(1);
        await Assert.That(note.LastError).Contains("SAGE");
        await Assert.That(issue.Notes).IsEmpty();
        await Assert.That(StepOf(forwardingCase, ForwardingStepKind.AwaitSageEntry).State).IsEqualTo(ForwardingStepState.Pending);
        await Assert.That(_cases.Get(forwardingCase.Id)!.Status).IsEqualTo(ForwardingCaseStatus.Failed);
        await Assert.That(mails.Count(m => m.To == "mv@bund.invalid")).IsEqualTo(1);
        await Assert.That(ApprovalMails(mails)).IsEmpty();
    }

    [Test]
    public async Task Note_waits_for_reduction_then_uses_rejection_default() {
        var application = SeedApplication(SelectedValuation.Reduced, DueSelectionStatus.Pending);
        var forwardingCase = SeedApplicationCase(application);
        var reductionCase = new ForwardingCase {
            Kind = ForwardingCaseKind.DueSelection,
            MembershipApplicationId = application.Id,
            DueSelectionId = application.DueSelectionId,
            CreatedAt = _now
        };
        _cases.Create(reductionCase, [
            new() { Order = 1, Kind = ForwardingStepKind.CreateTicket, TargetChapterId = _federal.Id, Purpose = ForwardingPurpose.Reduction, State = ForwardingStepState.Done, UpdatedAt = _now }
        ]);

        var issue = await DecideAsync(application, ApplicationStatus.Approved);
        var waitingNote = StepOf(forwardingCase, ForwardingStepKind.AddOutcomeNote);
        _dueSelections.UpdateStatus(application.DueSelectionId!.Value, DueSelectionStatus.Rejected, null);
        _hook.OnRecordDecided(ForwardingCaseKind.DueSelection, application.DueSelectionId.Value, _now.AddMinutes(2));
        await _job.RunAsync(_now.AddMinutes(3), CancellationToken.None);

        await Assert.That(waitingNote.State).IsEqualTo(ForwardingStepState.Waiting);
        await Assert.That(waitingNote.Attempts).IsEqualTo(0);
        var note = issue.Notes.Single();
        await Assert.That(note).Contains("Beitrag 2026 anteilig ab Oktober: 30,00 €");
        await Assert.That(note).Contains("Jahresbeitrag: 120,00 €");
        await Assert.That(note).Contains("Minderung auf 36,00 € (für ein Jahr): abgelehnt");
        await Assert.That(StepOf(forwardingCase, ForwardingStepKind.AddOutcomeNote).State).IsEqualTo(ForwardingStepState.Done);
    }

    [Test]
    public async Task Outside_reduction_lists_both_amounts() {
        var application = SeedApplication(SelectedValuation.Reduced, DueSelectionStatus.Forwarded);
        SeedApplicationCase(application);

        var issue = await DecideAsync(application, ApplicationStatus.Approved);

        var note = issue.Notes.Single();
        await Assert.That(note).Contains("Beitrag 2026 anteilig ab Oktober: 9,00 € bei Genehmigung der Minderung, sonst 30,00 €");
        await Assert.That(note).Contains("Minderung auf 36,00 € (für ein Jahr): offen");
    }

    [Test]
    public async Task Check_interval_comes_from_setting() {
        _options.SetValue(ForwardingOptionKeys.SageCheckMinutes, null, "30");
        var application = SeedApplication();
        var forwardingCase = SeedApplicationCase(application);

        await DecideAsync(application, ApplicationStatus.Approved);

        await Assert.That(StepOf(forwardingCase, ForwardingStepKind.AwaitSageEntry).NextAttemptAt).IsEqualTo(_now.AddMinutes(31));
    }

    [Test]
    [Arguments(false, DueSelectionStatus.Approved, "9,00 €", "genehmigt")]
    [Arguments(true, DueSelectionStatus.Rejected, "30,00 €", "abgelehnt")]
    public async Task Reduction_without_its_own_case_releases_the_note(bool viaMotion, DueSelectionStatus reductionResult, string partialAmount, string resultLabel) {
        var application = SeedApplication(SelectedValuation.Reduced, DueSelectionStatus.Pending);
        var forwardingCase = SeedApplicationCase(application);
        var dueRecordId = application.DueSelectionId!.Value;
        var motion = Motion.Create(_state.Id, "Bea", ApplicantEmail, "Beitragsminderung", "x", "<p>x</p>", _now, null, dueRecordId);
        _motions.Create(motion);

        var issue = await DecideAsync(application, ApplicationStatus.Approved);
        var waitingNote = StepOf(forwardingCase, ForwardingStepKind.AddOutcomeNote);
        if (viaMotion) {
            _motions.UpdatePendingApprovalStatus(motion.Id, reductionResult == DueSelectionStatus.Approved ? MotionApprovalStatus.Approved : MotionApprovalStatus.Rejected);
            _hook.OnMotionResolved(motion.Id, _now.AddMinutes(2));
        } else {
            _dueSelections.UpdateStatus(dueRecordId, reductionResult, null);
            _hook.OnRecordDecided(ForwardingCaseKind.DueSelection, dueRecordId, _now.AddMinutes(2));
        }
        await _job.RunAsync(_now.AddMinutes(3), CancellationToken.None);
        var mailsBeforeEntry = ApprovalMails(Mails()).Count;
        issue.StatusId = 5;
        await _job.RunAsync(_now.AddMinutes(18), CancellationToken.None);

        await Assert.That(_cases.ListByRecord(ForwardingCaseKind.DueSelection, dueRecordId)).IsEmpty();
        await Assert.That(waitingNote.State).IsEqualTo(ForwardingStepState.Waiting);
        await Assert.That(issue.Notes.Single()).Contains($"Beitrag 2026 anteilig ab Oktober: {partialAmount}");
        await Assert.That(issue.Notes.Single()).Contains($"Minderung auf 36,00 € (für ein Jahr): {resultLabel}");
        await Assert.That(mailsBeforeEntry).IsEqualTo(0);
        await Assert.That(ApprovalMails(Mails()).Count).IsEqualTo(1);
        await Assert.That(_cases.Get(forwardingCase.Id)!.Status).IsEqualTo(ForwardingCaseStatus.Done);
    }

    [Test]
    public async Task Note_and_check_proceed_after_application_forwarding_is_switched_off() {
        var application = SeedApplication();
        var forwardingCase = SeedApplicationCase(application);
        await _job.RunAsync(_now, CancellationToken.None);
        _applications.UpdateStatus(application.Id, ApplicationStatus.Approved, null);
        _hook.OnRecordDecided(ForwardingCaseKind.Application, application.Id, _now);

        _options.SetValue(ForwardingOptionKeys.MembershipApplicationEnabled, null, "false");
        await _job.RunAsync(_now.AddMinutes(1), CancellationToken.None);
        var issue = FakeRedmineClient.Issues.Single();
        issue.StatusId = 5;
        await _job.RunAsync(_now.AddMinutes(16), CancellationToken.None);

        var mails = Mails();
        await Assert.That(issue.Notes.Single()).Contains("Bitte um Eintragung in SAGE");
        await Assert.That(issue.AssigneeId).IsEqualTo(12);
        await Assert.That(mails.Count(m => m.To == "mv@bund.invalid")).IsEqualTo(0);
        await Assert.That(ApprovalMails(mails).Count).IsEqualTo(1);
        await Assert.That(_cases.Get(forwardingCase.Id)!.Status).IsEqualTo(ForwardingCaseStatus.Done);
    }

    [Test]
    public async Task Vote_closed_without_action_keeps_the_application_open_for_a_later_decision() {
        var application = SeedApplication();
        var motion = Motion.Create(_state.Id, "Bea", ApplicantEmail, "Mitgliedsantrag", "x", "<p>x</p>", _now, application.Id, null);
        _motions.Create(motion);
        var forwardingCase = SeedApplicationCase(application, motion.Id);
        await _job.RunAsync(_now, CancellationToken.None);
        var issue = FakeRedmineClient.Issues.Single();

        _motions.UpdatePendingApprovalStatus(motion.Id, MotionApprovalStatus.ClosedWithoutAction);
        _hook.OnMotionResolved(motion.Id, _now);
        await _job.RunAsync(_now.AddMinutes(1), CancellationToken.None);
        var noteAfterClose = StepOf(forwardingCase, ForwardingStepKind.AddOutcomeNote).State;
        var awaitAfterClose = StepOf(forwardingCase, ForwardingStepKind.AwaitSageEntry).State;
        var notesAfterClose = issue.Notes.Count;

        _applications.UpdateStatus(application.Id, ApplicationStatus.Approved, null);
        _hook.OnRecordDecided(ForwardingCaseKind.Application, application.Id, _now.AddMinutes(2));
        await _job.RunAsync(_now.AddMinutes(3), CancellationToken.None);
        var mailsBeforeEntry = ApprovalMails(Mails()).Count;
        issue.StatusId = 5;
        await _job.RunAsync(_now.AddMinutes(20), CancellationToken.None);

        await Assert.That(noteAfterClose).IsEqualTo(ForwardingStepState.Waiting);
        await Assert.That(awaitAfterClose).IsEqualTo(ForwardingStepState.Waiting);
        await Assert.That(notesAfterClose).IsEqualTo(0);
        var note = issue.Notes.Single();
        await Assert.That(note).Contains("angenommen");
        await Assert.That(note).DoesNotContain("ohne Beschluss");
        await Assert.That(note).Contains("Bitte um Eintragung in SAGE");
        await Assert.That(note).Contains($"/Administration/MembershipApplications/{application.Id}");
        await Assert.That(mailsBeforeEntry).IsEqualTo(0);
        await Assert.That(ApprovalMails(Mails()).Count).IsEqualTo(1);
        await Assert.That(_cases.Get(forwardingCase.Id)!.Status).IsEqualTo(ForwardingCaseStatus.Done);
    }

    [Test]
    public async Task Released_steps_of_an_undecided_application_go_back_to_waiting() {
        var application = SeedApplication();
        var motion = Motion.Create(_state.Id, "Bea", ApplicantEmail, "Mitgliedsantrag", "x", "<p>x</p>", _now, application.Id, null);
        _motions.Create(motion);
        var forwardingCase = SeedApplicationCase(application, motion.Id);
        await _job.RunAsync(_now, CancellationToken.None);
        _motions.UpdatePendingApprovalStatus(motion.Id, MotionApprovalStatus.ClosedWithoutAction);
        foreach (var step in _cases.GetSteps(forwardingCase.Id).Where(s => s.State == ForwardingStepState.Waiting)) {
            step.State = ForwardingStepState.Pending;
            _cases.UpdateStep(step, ForwardingStepState.Waiting);
        }

        await _job.RunAsync(_now.AddMinutes(1), CancellationToken.None);

        await Assert.That(StepOf(forwardingCase, ForwardingStepKind.AddOutcomeNote).State).IsEqualTo(ForwardingStepState.Waiting);
        await Assert.That(FakeRedmineClient.Issues.Single().Notes).IsEmpty();
        await Assert.That(_cases.Get(forwardingCase.Id)!.Status).IsEqualTo(ForwardingCaseStatus.Open);
    }

    [Test]
    public async Task Released_sage_step_of_an_undecided_application_goes_back_to_waiting() {
        var application = SeedApplication();
        var forwardingCase = SeedApplicationCase(application);
        await _job.RunAsync(_now, CancellationToken.None);
        var note = StepOf(forwardingCase, ForwardingStepKind.AddOutcomeNote);
        note.State = ForwardingStepState.Done;
        _cases.UpdateStep(note);
        var sageStep = StepOf(forwardingCase, ForwardingStepKind.AwaitSageEntry);
        sageStep.State = ForwardingStepState.Pending;
        _cases.UpdateStep(sageStep, ForwardingStepState.Waiting);

        await _job.RunAsync(_now.AddMinutes(1), CancellationToken.None);

        await Assert.That(StepOf(forwardingCase, ForwardingStepKind.AwaitSageEntry).State).IsEqualTo(ForwardingStepState.Waiting);
    }

    [Test]
    public async Task Approval_without_sage_step_writes_the_plain_outcome_note() {
        var application = SeedApplication();
        var forwardingCase = SeedApplicationCase(application, withSageStep: false);

        var issue = await DecideAsync(application, ApplicationStatus.Approved);

        var note = issue.Notes.Single();
        await Assert.That(note).Contains("angenommen");
        await Assert.That(note).DoesNotContain("SAGE");
        await Assert.That(note).DoesNotContain("Aufnahmebestätigung");
        await Assert.That(issue.StatusId).IsEqualTo(4);
        await Assert.That(issue.AssigneeId).IsEqualTo(77);
        await Assert.That(_cases.Get(forwardingCase.Id)!.Status).IsEqualTo(ForwardingCaseStatus.Done);
    }

    [Test]
    public async Task Ticket_already_on_the_done_status_fails_the_approval_note() {
        var application = SeedApplication();
        var forwardingCase = SeedApplicationCase(application);
        await _job.RunAsync(_now, CancellationToken.None);
        var issue = FakeRedmineClient.Issues.Single();
        issue.StatusId = 5;

        _applications.UpdateStatus(application.Id, ApplicationStatus.Approved, null);
        _hook.OnRecordDecided(ForwardingCaseKind.Application, application.Id, _now);
        await _job.RunAsync(_now.AddMinutes(1), CancellationToken.None);

        var note = StepOf(forwardingCase, ForwardingStepKind.AddOutcomeNote);
        var mails = Mails();
        await Assert.That(note.State).IsEqualTo(ForwardingStepState.Failed);
        await Assert.That(note.Attempts).IsEqualTo(1);
        await Assert.That(note.LastError).IsEqualTo("Das Ticket der Bundesebene steht schon auf dem Erledigt-Status. Bitte den Status des Tickets oder den Erledigt-Status in der Weiterleitung ändern und den Vorgang erneut versuchen.");
        await Assert.That(issue.Notes).IsEmpty();
        await Assert.That(StepOf(forwardingCase, ForwardingStepKind.AwaitSageEntry).State).IsEqualTo(ForwardingStepState.Pending);
        await Assert.That(_cases.Get(forwardingCase.Id)!.Status).IsEqualTo(ForwardingCaseStatus.Failed);
        await Assert.That(mails.Count(m => m.To == "mv@bund.invalid")).IsEqualTo(1);
        await Assert.That(ApprovalMails(mails)).IsEmpty();
    }
}
