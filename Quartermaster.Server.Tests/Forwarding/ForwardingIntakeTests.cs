using System.Text.Json;
using LinqToDB;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.Forwarding;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.Forwarding;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Motions;
using Quartermaster.Data.Notifications;
using Quartermaster.Data.Options;
using Quartermaster.Server.Forwarding;
using Quartermaster.Server.MembershipApplications;
using Quartermaster.Server.Notifications;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Forwarding;

public class ForwardingIntakeTests : RepositoryTestBase {
    private TestDataBuilder _builder = default!;
    private ForwardingSettingsRepository _settings = default!;
    private ForwardingCaseRepository _cases = default!;
    private OptionRepository _options = default!;
    private ForwardingIntake _intake = default!;
    private CapturingNotificationQueue _notifications = default!;
    private Chapter _federal = default!;
    private Chapter _state = default!;
    private Chapter _district = default!;

    [Before(Test)]
    public void Setup() {
        _builder = new TestDataBuilder(Db);
        _settings = new ForwardingSettingsRepository(Db);
        _cases = new ForwardingCaseRepository(Db);
        _options = new OptionRepository(Db, AuditLog);
        _options.SupplementDefaults();
        _options.SetValue(ForwardingOptionKeys.DueSelectionEnabled, null, "true");
        _options.SetValue(ForwardingOptionKeys.MembershipApplicationEnabled, null, "true");
        var chapters = new ChapterRepository(Db, AuditLog);
        var motions = new MotionRepository(Db, AuditLog);
        var dueSelections = new DueSelectionRepository(Db, AuditLog);
        _notifications = new CapturingNotificationQueue();
        var review = new ApplicationReviewService(motions, chapters, new CapturingNotificationQueue());
        _intake = new ForwardingIntake(
            new ForwardingSettingsResolver(chapters, _settings, new RedmineConnectionRepository(Db)),
            _cases, dueSelections, new MembershipApplicationRepository(Db, AuditLog), review, _options, _notifications, new NotificationLogRepository(Db), chapters);
        _federal = _builder.SeedChapter("Bund " + Guid.NewGuid().ToString("N")[..6]);
        _state = _builder.SeedChapter("Land", parentChapterId: _federal.Id);
        _district = _builder.SeedChapter("Kreis", parentChapterId: _state.Id);
    }

    private static DueSelectionDTO Regular() => new() {
        FirstName = "Anna", LastName = "Pirat", Email = "anna@example.org", MemberNumber = 4711,
        SelectedValuation = SelectedValuation.MonthlyPayGroup, SelectedDue = 120, MonthlyIncomeGroup = 2000
    };

    private static DueSelectionDTO Reduction(decimal amount = 36) => new() {
        FirstName = "Anna", LastName = "Pirat", Email = "anna@example.org", MemberNumber = 4711,
        SelectedValuation = SelectedValuation.Reduced, ReducedAmount = amount, ReducedJustification = "Studium"
    };

    private static MembershipApplicationDTO Application(DueSelectionDTO dues) => new() {
        FirstName = "Bea", LastName = "Neu", Email = "bea@example.org", DateOfBirth = new DateOnly(2000, 1, 1),
        AddressStreet = "Weg", AddressHouseNbr = "1", AddressPostCode = "30159", AddressCity = "Hannover",
        EntryDate = new DateOnly(2026, 11, 1), DueSelection = dues, ConformityDeclarationAccepted = true
    };

    [Test]
    public async Task Regular_due_creates_case_without_record() {
        var result = _intake.IntakeDueSelection(Regular(), _district.Id, null, "anna", false, DateTime.UtcNow);

        var forwardingCase = _cases.Get(result.CaseId!.Value)!;
        var steps = _cases.GetSteps(forwardingCase.Id);
        await Assert.That(result.DueSelectionId).IsNull();
        await Assert.That(steps.Single().TargetChapterId).IsEqualTo(_federal.Id);
        await Assert.That(steps.Single().Purpose).IsEqualTo(ForwardingPurpose.Regular);
        await Assert.That(forwardingCase.StateChapterId).IsEqualTo(_state.Id);
    }

    [Test]
    public async Task Reduction_quartermaster_creates_record_motion_and_waiting_note() {
        _settings.SaveChapter(new ForwardingChapterSetting { ChapterId = _state.Id, ReductionMode = ForwardingMode.Quartermaster }, []);

        var result = _intake.IntakeDueSelection(Reduction(), _district.Id, null, "anna", false, DateTime.UtcNow);

        var record = Db.DueSelections.Single(d => d.Id == result.DueSelectionId);
        var motion = Db.Motions.Single(m => m.LinkedDueSelectionId == record.Id);
        var forwardingCase = _cases.Get(result.CaseId!.Value)!;
        var steps = _cases.GetSteps(forwardingCase.Id);
        await Assert.That(record.ChapterId).IsEqualTo(_state.Id);
        await Assert.That(record.Status).IsEqualTo(DueSelectionStatus.Pending);
        await Assert.That(motion.ChapterId).IsEqualTo(_state.Id);
        await Assert.That(motion.LinkedMembershipApplicationId).IsNull();
        await Assert.That(forwardingCase.MotionId).IsEqualTo(motion.Id);
        await Assert.That(steps.Select(s => (s.Kind, s.State)).ToArray()).IsEquivalentTo(new[] {
            (ForwardingStepKind.CreateTicket, ForwardingStepState.Pending),
            (ForwardingStepKind.AddOutcomeNote, ForwardingStepState.Waiting)
        });
    }

    [Test]
    public async Task Application_quartermaster_creates_application_motion_without_reduction() {
        _settings.SaveChapter(new ForwardingChapterSetting { ChapterId = _district.Id, ApplicationMode = ForwardingMode.Quartermaster }, []);

        var result = _intake.IntakeApplication(Application(Regular()), _district.Id, DateTime.UtcNow);

        var application = Db.MembershipApplications.Single(a => a.Id == result.ApplicationId);
        var motion = Db.Motions.Single(m => m.LinkedMembershipApplicationId == application.Id);
        var dues = Db.DueSelections.Single(d => d.Id == application.DueSelectionId);
        await Assert.That(application.ChapterId).IsEqualTo(_district.Id);
        await Assert.That(motion.ChapterId).IsEqualTo(_district.Id);
        await Assert.That(motion.LinkedDueSelectionId).IsNull();
        await Assert.That(dues.Status).IsEqualTo(DueSelectionStatus.AutoApproved);
        await Assert.That(_cases.GetSteps(result.CaseId!.Value).Count(s => s.Kind == ForwardingStepKind.AddOutcomeNote)).IsEqualTo(1);
    }

    [Test]
    public async Task Application_redmine_stores_nothing() {
        _settings.SaveChapter(new ForwardingChapterSetting { ChapterId = _state.Id, ApplicationMode = ForwardingMode.Redmine }, []);

        var result = _intake.IntakeApplication(Application(Regular()), _district.Id, DateTime.UtcNow);

        var steps = _cases.GetSteps(result.CaseId!.Value);
        await Assert.That(result.ApplicationId).IsNull();
        await Assert.That(steps.Select(s => s.TargetChapterId).ToArray()).IsEquivalentTo(new[] { _federal.Id, _state.Id });
        await Assert.That(steps[0].TargetChapterId).IsEqualTo(_federal.Id);
    }

    [Test]
    public async Task Application_with_reduction_creates_two_linked_cases() {
        _settings.SaveChapter(new ForwardingChapterSetting { ChapterId = _district.Id, ApplicationMode = ForwardingMode.Quartermaster }, []);
        _settings.SaveChapter(new ForwardingChapterSetting { ChapterId = _federal.Id, ReductionMode = ForwardingMode.Redmine }, []);

        var result = _intake.IntakeApplication(Application(Reduction(6)), _district.Id, DateTime.UtcNow);

        var applicationCase = _cases.Get(result.CaseId!.Value)!;
        var reductionCase = _cases.Get(applicationCase.RelatedCaseId!.Value)!;
        var application = Db.MembershipApplications.Single(a => a.Id == result.ApplicationId);
        var dues = Db.DueSelections.Single(d => d.Id == application.DueSelectionId);
        await Assert.That(reductionCase.RelatedCaseId).IsEqualTo(applicationCase.Id);
        await Assert.That(reductionCase.DecidingChapterId).IsEqualTo(_federal.Id);
        await Assert.That(_cases.GetSteps(reductionCase.Id).Single().Purpose).IsEqualTo(ForwardingPurpose.Reduction);
        await Assert.That(dues.Status).IsEqualTo(DueSelectionStatus.Forwarded);
        await Assert.That(dues.ProcessedAt).IsNotNull();
        await Assert.That(Db.Motions.Count(m => m.LinkedDueSelectionId == dues.Id)).IsEqualTo(0);
    }

    [Test]
    public async Task Switches_off_stores_records_like_today() {
        _options.SetValue(ForwardingOptionKeys.DueSelectionEnabled, null, "false");
        _options.SetValue(ForwardingOptionKeys.MembershipApplicationEnabled, null, "false");

        var due = _intake.IntakeDueSelection(Regular(), _district.Id, null, "anna", false, DateTime.UtcNow);
        var application = _intake.IntakeApplication(Application(Regular()), _district.Id, DateTime.UtcNow);

        await Assert.That(due.CaseId).IsNull();
        await Assert.That(Db.DueSelections.Single(d => d.Id == due.DueSelectionId).Status).IsEqualTo(DueSelectionStatus.AutoApproved);
        await Assert.That(application.CaseId).IsNull();
        await Assert.That(Db.Motions.Count(m => m.LinkedMembershipApplicationId == application.ApplicationId)).IsEqualTo(1);
    }

    [Test]
    public async Task Payload_holds_submitted_data() {
        var result = _intake.IntakeDueSelection(Reduction(), _district.Id, null, "anna", true, DateTime.UtcNow);

        var payload = JsonSerializer.Deserialize<ForwardingPayload>(_cases.Get(result.CaseId!.Value)!.PayloadJson!)!;
        await Assert.That(payload.DueSelection!.ReducedJustification).IsEqualTo("Studium");
        await Assert.That(payload.EnteredByHand).IsTrue();
        await Assert.That(payload.SubmittedByName).IsEqualTo("anna");
    }
    private List<Guid> NotifiedChapters() {
        return _notifications.Requests
            .Where(r => r.Payload is DueSelectionSubmittedPayload)
            .Select(r => ((DueSelectionSubmittedPayload)r.Payload).ChapterId)
            .ToList();
    }

    [Test]
    public async Task Deciding_chapter_of_an_application_reduction_is_notified() {
        _settings.SaveChapter(new ForwardingChapterSetting { ChapterId = _district.Id, ApplicationMode = ForwardingMode.Quartermaster }, []);
        _settings.SaveChapter(new ForwardingChapterSetting { ChapterId = _state.Id, ReductionMode = ForwardingMode.Quartermaster }, []);

        _intake.IntakeApplication(Application(Reduction(36)), _district.Id, DateTime.UtcNow);

        await Assert.That(NotifiedChapters()).IsEquivalentTo(new[] { _state.Id });
    }

    [Test]
    public async Task Federal_reduction_motion_is_notified() {
        _settings.SaveChapter(new ForwardingChapterSetting { ChapterId = _federal.Id, ReductionMode = ForwardingMode.Quartermaster }, []);

        _intake.IntakeDueSelection(Reduction(6), _district.Id, null, "anna", false, DateTime.UtcNow);

        await Assert.That(NotifiedChapters()).IsEquivalentTo(new[] { _federal.Id });
    }
    private ForwardingIntake IntakeWith(INotificationDispatchQueue notifications) {
        var chapters = new ChapterRepository(Db, AuditLog);
        var dueSelections = new DueSelectionRepository(Db, AuditLog);
        var review = new ApplicationReviewService(new MotionRepository(Db, AuditLog), chapters, notifications);
        return new ForwardingIntake(
            new ForwardingSettingsResolver(chapters, _settings, new RedmineConnectionRepository(Db)),
            _cases, dueSelections, new MembershipApplicationRepository(Db, AuditLog), review, _options, notifications, new NotificationLogRepository(Db), chapters);
    }

    [Test]
    public async Task Failure_midway_leaves_nothing_behind() {
        _settings.SaveChapter(new ForwardingChapterSetting { ChapterId = _state.Id, ReductionMode = ForwardingMode.Quartermaster }, []);
        var intake = IntakeWith(new ThrowingNotificationQueue());

        await Assert.That(() => intake.IntakeDueSelection(Reduction(), _district.Id, null, "anna", false, DateTime.UtcNow)).Throws<InvalidOperationException>();

        await Assert.That(Db.DueSelections.Count()).IsEqualTo(0);
        await Assert.That(Db.Motions.Count()).IsEqualTo(0);
        await Assert.That(Db.ForwardingCases.Count()).IsEqualTo(0);
    }

    [Test]
    public async Task Reduction_of_an_application_waiting_for_its_chapter_is_held_back() {
        _options.SetValue(ForwardingOptionKeys.DueSelectionEnabled, null, "false");
        _options.SetValue(ForwardingOptionKeys.MembershipApplicationEnabled, null, "false");

        var result = _intake.IntakeApplication(Application(Reduction(36)), null, DateTime.UtcNow);

        var application = Db.MembershipApplications.Single(a => a.Id == result.ApplicationId);
        var dues = Db.DueSelections.Single(d => d.Id == application.DueSelectionId);
        await Assert.That(application.Status).IsEqualTo(ApplicationStatus.PendingDivisionLinking);
        await Assert.That(dues.Status).IsEqualTo(DueSelectionStatus.Pending);
        await Assert.That(dues.ChapterId).IsNull();
        await Assert.That(Db.Motions.Count()).IsEqualTo(0);
    }

    [Test]
    public async Task Linking_routes_the_held_back_reduction_to_the_state() {
        _options.SetValue(ForwardingOptionKeys.DueSelectionEnabled, null, "false");
        _options.SetValue(ForwardingOptionKeys.MembershipApplicationEnabled, null, "false");
        var result = _intake.IntakeApplication(Application(Reduction(36)), null, DateTime.UtcNow);
        var application = Db.MembershipApplications.Single(a => a.Id == result.ApplicationId);

        _intake.LinkApplication(application, null, _district.Id, DateTime.UtcNow);

        var linked = Db.MembershipApplications.Single(a => a.Id == application.Id);
        var dues = Db.DueSelections.Single(d => d.Id == application.DueSelectionId);
        await Assert.That(linked.ChapterId).IsEqualTo(_district.Id);
        await Assert.That(linked.Status).IsEqualTo(ApplicationStatus.Pending);
        await Assert.That(Db.Motions.Single(m => m.LinkedMembershipApplicationId == application.Id).ChapterId).IsEqualTo(_district.Id);
        await Assert.That(Db.Motions.Single(m => m.LinkedDueSelectionId == dues.Id).ChapterId).IsEqualTo(_state.Id);
        await Assert.That(dues.ChapterId).IsEqualTo(_state.Id);
    }

    [Test]
    public async Task Linking_with_forwarding_hands_the_application_to_the_chapter_redmine() {
        _settings.SaveChapter(new ForwardingChapterSetting { ChapterId = _district.Id, ApplicationMode = ForwardingMode.Redmine }, [
            new ForwardingTarget { Purpose = ForwardingPurpose.Application, ProjectId = 4, TrackerId = 1 }
        ]);
        var result = _intake.IntakeApplication(Application(Regular()), null, DateTime.UtcNow);
        var application = Db.MembershipApplications.Single(a => a.Id == result.ApplicationId);
        _cases.ClearPayload(result.CaseId!.Value);

        _intake.LinkApplication(application, null, _district.Id, DateTime.UtcNow);

        var forwardingCase = _cases.Get(result.CaseId!.Value)!;
        var steps = _cases.GetSteps(forwardingCase.Id);
        await Assert.That(steps.Select(s => (s.Order, s.TargetChapterId, s.State)).ToArray()).IsEquivalentTo(new[] {
            (1, _federal.Id, ForwardingStepState.Pending),
            (2, _district.Id, ForwardingStepState.Pending)
        });
        await Assert.That(forwardingCase.PayloadJson).Contains("Bea");
        await Assert.That(forwardingCase.MembershipApplicationId).IsNull();
        await Assert.That(Db.MembershipApplications.Count()).IsEqualTo(0);
        await Assert.That(Db.DueSelections.Count()).IsEqualTo(0);
        await Assert.That(Db.Motions.Count()).IsEqualTo(0);
    }

    [Test]
    public async Task Linking_with_forwarding_and_quartermaster_mode_waits_for_the_vote() {
        _settings.SaveChapter(new ForwardingChapterSetting { ChapterId = _state.Id, ApplicationMode = ForwardingMode.Quartermaster }, []);
        var result = _intake.IntakeApplication(Application(Regular()), null, DateTime.UtcNow);
        var application = Db.MembershipApplications.Single(a => a.Id == result.ApplicationId);

        _intake.LinkApplication(application, null, _district.Id, DateTime.UtcNow);

        var forwardingCase = _cases.Get(result.CaseId!.Value)!;
        var motion = Db.Motions.Single(m => m.LinkedMembershipApplicationId == application.Id);
        await Assert.That(motion.ChapterId).IsEqualTo(_state.Id);
        await Assert.That(Db.MembershipApplications.Single(a => a.Id == application.Id).ChapterId).IsEqualTo(_state.Id);
        await Assert.That(forwardingCase.MotionId).IsEqualTo(motion.Id);
        await Assert.That(forwardingCase.DecidingChapterId).IsEqualTo(_state.Id);
        await Assert.That(_cases.GetSteps(forwardingCase.Id).Last().Kind).IsEqualTo(ForwardingStepKind.AddOutcomeNote);
    }

    [Test]
    public async Task Application_motion_mentions_a_separate_decision_only_for_a_real_reduction() {
        _settings.SaveChapter(new ForwardingChapterSetting { ChapterId = _district.Id, ApplicationMode = ForwardingMode.Quartermaster }, []);

        var high = _intake.IntakeApplication(Application(Reduction(80)), _district.Id, DateTime.UtcNow);
        var low = _intake.IntakeApplication(Application(Reduction(36)), _district.Id, DateTime.UtcNow);

        var highText = Db.Motions.Single(m => m.LinkedMembershipApplicationId == high.ApplicationId).TextMarkdown;
        var lowText = Db.Motions.Single(m => m.LinkedMembershipApplicationId == low.ApplicationId).TextMarkdown;
        await Assert.That(highText).DoesNotContain("gesondert entschieden");
        await Assert.That(lowText).Contains("gesondert entschieden");
    }
}
