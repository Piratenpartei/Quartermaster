using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Quartermaster.Api;
using Quartermaster.Api.ApplicantRegister;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.Forwarding;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Api.Motions;
using Quartermaster.Data;
using Quartermaster.Data.ApplicantRegister;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Forwarding;
using Quartermaster.Data.Members;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Motions;
using Quartermaster.Server.ApplicantRegister;
using Quartermaster.Server.Forwarding;
using Quartermaster.Server.Forwarding.SageEntry;
using Quartermaster.Server.Tests.Forwarding.Fakes;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.ApplicantRegister;

[NotInParallel("FakeRedmineClient")]
public class FederalApprovalFlowTests : IntegrationTestBase {
    private const string ApprovedTriggerId = "application_approved";
    private const string RejectedTriggerId = "application_rejected";
    private const string SubmittedTriggerId = "application_submitted";
    private const string FederalProcessorEmail = "bund@test.local";

    private Chapter _root = default!;
    private Chapter _state = default!;
    private Member _rootOfficer = default!;
    private Member _stateOfficer = default!;
    private ApplicantRegisterEntry _entry = default!;
    private string _voterToken = "";
    private string _rootProcessorToken = "";
    private string _stateProcessorToken = "";

    public FederalApprovalFlowTests() {
        DbContext.SupplementDefaults(Factory.Services);
        FakeRedmineClient.Reset();
    }

    private static MembershipApplicationDTO Dto(Guid divisionId, bool selfDeclared = false, bool reduced = false) => new() {
        FirstName = "Bea",
        LastName = "Neu",
        DateOfBirth = new DateOnly(1995, 3, 3),
        Citizenship = "DE",
        Email = "bea@test.local",
        PhoneNumber = "0123",
        AddressStreet = "Weg",
        AddressHouseNbr = "1",
        AddressPostCode = "30159",
        AddressCity = "Hannover",
        AddressAdministrativeDivisionId = divisionId,
        HasPriorDeclinedApplication = selfDeclared,
        ConformityDeclarationAccepted = true,
        EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
        DueSelection = reduced ? ReducedDues() : null
    };

    private static DueSelectionDTO ReducedDues() => new() {
        SelectedValuation = SelectedValuation.Reduced,
        ReducedAmount = 36,
        ReducedJustification = "Studium",
        ReducedTimeSpan = ReducedTimeSpan.OneYear,
        SelectedDue = 36,
        IsDirectDeposit = true,
        AccountHolder = "Bea Neu",
        IBAN = "DE02120300000000202051",
        PaymentSchedule = PaymentSchedule.Annual
    };

    private static string Today() {
        return BerlinTime.FromUtc(DateTime.UtcNow).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
    }

    /// <summary>Seeds Bund above Land with Land deciding in Quartermaster, a matching register entry, officers and processors, and submits the application.</summary>
    private async Task<Guid> SubmitMatchingApplicationAsync(bool selfDeclaredOnly = false, bool reduced = false) {
        _root = Builder.SeedChapter("Bund");
        _state = Builder.SeedChapter("Land", parentChapterId: _root.Id);
        SageSetupSeeder.Seed(Factory.Services, _root.Id, _state.Id);
        if (!selfDeclaredOnly) {
            _entry = Builder.SeedRegisterEntry(
                chapterId: _root.Id,
                firstName: "Bea",
                lastName: "Neu",
                dateOfBirth: new DateTime(1995, 3, 3),
                street: "Anderswo",
                postCode: "99999",
                city: "Woanders",
                email: "bea@test.local",
                occurredOn: new DateTime(2024, 6, 7),
                note: "Geheimnotiz");
        }

        _rootOfficer = Builder.SeedMember(_root.Id, firstName: "Bundes", lastName: "Vorstand");
        Builder.SeedChapterOfficer(_rootOfficer.Id, _root.Id);
        _stateOfficer = Builder.SeedMember(_state.Id, firstName: "Landes", lastName: "Vorstand");
        Builder.SeedChapterOfficer(_stateOfficer.Id, _state.Id);
        (_, _voterToken) = Builder.SeedAuthenticatedUser(globalPermissions: new[] { PermissionIdentifier.SystemVote });
        (_, _rootProcessorToken) = Builder.SeedAuthenticatedUser(
            email: FederalProcessorEmail,
            chapterPermissions: new() { [_root.Id] = new[] { PermissionIdentifier.ProcessApplications } });
        (_, _stateProcessorToken) = Builder.SeedAuthenticatedUser(
            email: "land@test.local",
            chapterPermissions: new() { [_state.Id] = new[] { PermissionIdentifier.ProcessApplications } });

        var (_, submitterToken) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(submitterToken);
        var response = await client.PostAsJsonAsync("/api/membershipapplications", Dto(Builder.SeedDivisionFor(_state.Id), selfDeclaredOnly, reduced));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        return Db.MembershipApplications.Single().Id;
    }

    private Motion ChapterMotion(Guid applicationId) {
        return Db.Motions.Single(m => m.LinkedMembershipApplicationId == applicationId && m.ChapterId == _state.Id);
    }

    private Motion? FederalMotion(Guid applicationId) {
        return Db.Motions.SingleOrDefault(m => m.LinkedMembershipApplicationId == applicationId && m.ChapterId == _root.Id);
    }

    private async Task<HttpStatusCode> TryVoteAsync(Guid motionId, Member officer, VoteType vote) {
        using var client = await AuthenticatedClientWithCsrfAsync(_voterToken);
        var response = await client.PostAsJsonAsync("/api/motions/vote", new MotionVoteRequest { MotionId = motionId, MemberId = officer.Id, Vote = vote });
        return response.StatusCode;
    }

    private async Task VoteAsync(Guid motionId, Member officer, VoteType vote) {
        await Assert.That(await TryVoteAsync(motionId, officer, vote)).IsEqualTo(HttpStatusCode.OK);
    }

    private async Task<HttpStatusCode> ProcessAsync(string token, Guid applicationId, ApplicationStatus status) {
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/admin/membershipapplications/process", new { Id = applicationId, Status = status });
        return response.StatusCode;
    }

    private async Task RunJobAsync(DateTime nowUtc) {
        using var scope = Factory.Services.CreateScope();
        var provider = scope.ServiceProvider;
        var redmine = new FakeRedmineClient();
        var job = ActivatorUtilities.CreateInstance<ForwardingJob>(provider, redmine,
            ActivatorUtilities.CreateInstance<SageEntryNote>(provider, redmine),
            ActivatorUtilities.CreateInstance<SageEntryCheck>(provider, redmine));
        await job.RunAsync(nowUtc, CancellationToken.None);
    }

    private ForwardingStep StepOf(Guid applicationId, ForwardingStepKind kind) {
        var forwardingCase = Db.ForwardingCases.Single(c => c.Kind == ForwardingCaseKind.Application && c.MembershipApplicationId == applicationId);
        return Db.ForwardingSteps.Single(s => s.CaseId == forwardingCase.Id && s.Kind == kind);
    }

    private ApplicantRegisterEntry? AutomaticEntry(Guid applicationId) {
        return Db.ApplicantRegisterEntries.SingleOrDefault(e => e.SourceApplicationId == applicationId);
    }

    private async Task WithFlowAsync(Func<FederalApprovalFlow, Task> action) {
        using var scope = Factory.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<FederalApprovalFlow>());
    }

    [Test]
    public async Task Chapter_approval_creates_federal_motion() {
        var applicationId = await SubmitMatchingApplicationAsync();
        var submittedBefore = Db.NotificationLogs.Count(l => l.TriggerId == SubmittedTriggerId && l.Recipient == FederalProcessorEmail);

        await VoteAsync(ChapterMotion(applicationId).Id, _stateOfficer, VoteType.Approve);

        var application = Db.MembershipApplications.Single(a => a.Id == applicationId);
        var federal = FederalMotion(applicationId);
        await Assert.That(application.Status).IsEqualTo(ApplicationStatus.PendingFederalApproval);
        await Assert.That(application.ProcessedAt).IsNull();
        await Assert.That(federal).IsNotNull();
        await Assert.That(application.FederalApprovalMotionId).IsEqualTo(federal!.Id);
        await Assert.That(federal.Title).IsEqualTo("Mitgliedsantrag (Zustimmung nach § 3 Abs. 2 BS): Bea Neu");
        await Assert.That(federal.TextMarkdown).Contains($"Anhörung: Gliederung Land hat am {Today()} zugestimmt (Ja: 1, Nein: 0, Enthaltung: 0)");
        await Assert.That(federal.TextMarkdown).Contains($"/Administration/MembershipApplications/{applicationId}");
        await Assert.That(federal.TextMarkdown).Contains("bea@test.local");
        await Assert.That(federal.TextMarkdown).DoesNotContain("Aufnahme abgelehnt");
        await Assert.That(federal.TextMarkdown).DoesNotContain("07.06.2024");
        await Assert.That(federal.TextMarkdown).DoesNotContain("Geheimnotiz");
        var submittedAfter = Db.NotificationLogs.Count(l => l.TriggerId == SubmittedTriggerId && l.Recipient == FederalProcessorEmail);
        await Assert.That(submittedAfter).IsEqualTo(submittedBefore + 1);
        var forwardingCase = Db.ForwardingCases.Single(c => c.MembershipApplicationId == applicationId);
        await Assert.That(forwardingCase.MotionId).IsEqualTo(federal.Id);
        await Assert.That(forwardingCase.DecidingChapterId).IsEqualTo(_root.Id);
    }

    [Test]
    public async Task Federal_submitted_notification_mentions_reduction() {
        var applicationId = await SubmitMatchingApplicationAsync(reduced: true);
        await Assert.That(Db.DueSelections.Count()).IsGreaterThan(0);
        var before = Db.NotificationLogs.Where(l => l.TriggerId == SubmittedTriggerId).Select(l => l.Id).ToList();

        await VoteAsync(ChapterMotion(applicationId).Id, _stateOfficer, VoteType.Approve);

        var federalLog = Db.NotificationLogs.Single(l => l.TriggerId == SubmittedTriggerId && l.Recipient == FederalProcessorEmail && !before.Contains(l.Id));
        await Assert.That(federalLog.Body!).Contains("Beitragsminderung");
    }

    [Test]
    public async Task Federal_submitted_notification_without_reduction_says_nothing_about_it() {
        var applicationId = await SubmitMatchingApplicationAsync();
        var before = Db.NotificationLogs.Where(l => l.TriggerId == SubmittedTriggerId).Select(l => l.Id).ToList();

        await VoteAsync(ChapterMotion(applicationId).Id, _stateOfficer, VoteType.Approve);

        var federalLog = Db.NotificationLogs.Single(l => l.TriggerId == SubmittedTriggerId && l.Recipient == FederalProcessorEmail && !before.Contains(l.Id));
        await Assert.That(federalLog.Body!).DoesNotContain("Beitragsminderung");
    }

    [Test]
    public async Task Federal_ticket_carries_register_notice_on_quartermaster_path() {
        await SubmitMatchingApplicationAsync();

        await RunJobAsync(DateTime.UtcNow);

        var description = FakeRedmineClient.Issues.Single().Description;
        await Assert.That(description).Contains(RegisterNoticeText.Header);
        await Assert.That(description).Contains("Aufnahme abgelehnt am 07.06.2024: Bea Neu");
        await Assert.That(description).DoesNotContain(RegisterNoticeText.SelfDeclarationLine);
    }

    [Test]
    public async Task Federal_ticket_carries_self_declaration_without_hit() {
        await SubmitMatchingApplicationAsync(selfDeclaredOnly: true);
        await Assert.That(Db.ApplicationRegisterHits.Count()).IsEqualTo(0);

        await RunJobAsync(DateTime.UtcNow);

        var description = FakeRedmineClient.Issues.Single().Description;
        await Assert.That(description).Contains(RegisterNoticeText.Header);
        await Assert.That(description).Contains(RegisterNoticeText.SelfDeclarationLine);
        await Assert.That(description).DoesNotContain("Aufnahme abgelehnt");
    }

    [Test]
    public async Task Federal_motion_created_once() {
        var applicationId = await SubmitMatchingApplicationAsync();
        var chapterMotionId = ChapterMotion(applicationId).Id;

        await VoteAsync(chapterMotionId, _stateOfficer, VoteType.Approve);
        await WithFlowAsync(flow => flow.OnMotionResolvedAsync(chapterMotionId));
        await WithFlowAsync(flow => flow.OnRecordDecidedAsync(applicationId, DateTime.UtcNow));

        await Assert.That(Db.Motions.Count(m => m.LinkedMembershipApplicationId == applicationId && m.ChapterId == _root.Id)).IsEqualTo(1);
        var federal = FederalMotion(applicationId)!;
        await Assert.That(Db.MembershipApplications.Single(a => a.Id == applicationId).FederalApprovalMotionId).IsEqualTo(federal.Id);
    }

    [Test]
    public async Task Chapter_approval_holds_mail_and_sage() {
        var applicationId = await SubmitMatchingApplicationAsync();
        await RunJobAsync(DateTime.UtcNow);

        await VoteAsync(ChapterMotion(applicationId).Id, _stateOfficer, VoteType.Approve);
        await RunJobAsync(DateTime.UtcNow.AddMinutes(1));

        await Assert.That(Db.NotificationLogs.Count(l => l.TriggerId == ApprovedTriggerId)).IsEqualTo(0);
        await Assert.That(StepOf(applicationId, ForwardingStepKind.AddOutcomeNote).State).IsEqualTo(ForwardingStepState.Waiting);
        await Assert.That(StepOf(applicationId, ForwardingStepKind.AwaitSageEntry).State).IsEqualTo(ForwardingStepState.Waiting);
        await Assert.That(FakeRedmineClient.Issues.Single().Notes.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Federal_approval_releases_mail_and_note() {
        var applicationId = await SubmitMatchingApplicationAsync();
        await RunJobAsync(DateTime.UtcNow);
        await VoteAsync(ChapterMotion(applicationId).Id, _stateOfficer, VoteType.Approve);
        await RunJobAsync(DateTime.UtcNow.AddMinutes(1));

        await VoteAsync(FederalMotion(applicationId)!.Id, _rootOfficer, VoteType.Approve);
        await RunJobAsync(DateTime.UtcNow.AddMinutes(2));

        var application = Db.MembershipApplications.Single(a => a.Id == applicationId);
        var issue = FakeRedmineClient.Issues.Single();
        await Assert.That(application.Status).IsEqualTo(ApplicationStatus.Approved);
        await Assert.That(application.ProcessedAt).IsNotNull();
        await Assert.That(issue.Notes.Single()).Contains("Vorstand Bund");
        await Assert.That(issue.Notes.Single()).DoesNotContain("Vorstand Land");
        await Assert.That(Db.NotificationLogs.Count(l => l.TriggerId == ApprovedTriggerId)).IsEqualTo(0);

        issue.StatusId = SageSetupSeeder.SageDoneStatusId;
        await RunJobAsync(DateTime.UtcNow.AddDays(2));

        await Assert.That(StepOf(applicationId, ForwardingStepKind.AwaitSageEntry).State).IsEqualTo(ForwardingStepState.Done);
        await Assert.That(Db.NotificationLogs.Count(l => l.TriggerId == ApprovedTriggerId && l.Recipient == "bea@test.local")).IsEqualTo(1);
    }

    [Test]
    public async Task Chapter_cannot_decide_the_federal_step() {
        var applicationId = await SubmitMatchingApplicationAsync();

        var chapterDecision = await ProcessAsync(_stateProcessorToken, applicationId, ApplicationStatus.Approved);
        var pending = Db.MembershipApplications.Single(a => a.Id == applicationId);
        var chapterOnFederalStep = await ProcessAsync(_stateProcessorToken, applicationId, ApplicationStatus.Approved);
        var federalDecision = await ProcessAsync(_rootProcessorToken, applicationId, ApplicationStatus.Approved);

        await Assert.That(chapterDecision).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(pending.Status).IsEqualTo(ApplicationStatus.PendingFederalApproval);
        await Assert.That(pending.ProcessedAt).IsNull();
        await Assert.That(FederalMotion(applicationId)!.TextMarkdown).Contains($"Anhörung: Gliederung Land hat am {Today()} im Bearbeitungsdialog zugestimmt");
        await Assert.That(chapterOnFederalStep).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(federalDecision).IsEqualTo(HttpStatusCode.OK);
        var approved = Db.MembershipApplications.Single(a => a.Id == applicationId);
        await Assert.That(approved.Status).IsEqualTo(ApplicationStatus.Approved);
        await Assert.That(approved.ProcessedAt).IsNotNull();
    }

    [Test]
    public async Task Chapter_motion_after_dialog_approval_leaves_federal_step_open() {
        var applicationId = await SubmitMatchingApplicationAsync();
        await ProcessAsync(_stateProcessorToken, applicationId, ApplicationStatus.Approved);
        var federalMotionId = FederalMotion(applicationId)!.Id;

        var vote = await TryVoteAsync(ChapterMotion(applicationId).Id, _stateOfficer, VoteType.Approve);

        var application = Db.MembershipApplications.Single(a => a.Id == applicationId);
        await Assert.That(vote).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(application.Status).IsEqualTo(ApplicationStatus.PendingFederalApproval);
        await Assert.That(application.ProcessedAt).IsNull();
        await Assert.That(application.FederalApprovalMotionId).IsEqualTo(federalMotionId);
        await Assert.That(Db.Motions.Count(m => m.LinkedMembershipApplicationId == applicationId && m.ChapterId == _root.Id)).IsEqualTo(1);
    }

    [Test]
    public async Task Dialog_rejection_closes_chapter_motion() {
        var applicationId = await SubmitMatchingApplicationAsync();
        var chapterMotionId = ChapterMotion(applicationId).Id;

        await ProcessAsync(_stateProcessorToken, applicationId, ApplicationStatus.Rejected);
        var closed = Db.Motions.Single(m => m.Id == chapterMotionId).ApprovalStatus;
        var vote = await TryVoteAsync(chapterMotionId, _stateOfficer, VoteType.Approve);

        await Assert.That(closed).IsEqualTo(MotionApprovalStatus.ClosedWithoutAction);
        await Assert.That(vote).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Db.Motions.Single(m => m.Id == chapterMotionId).ApprovalStatus).IsEqualTo(MotionApprovalStatus.ClosedWithoutAction);
        await Assert.That(Db.MembershipApplications.Single(a => a.Id == applicationId).Status).IsEqualTo(ApplicationStatus.Rejected);
        await Assert.That(Db.NotificationLogs.Count(l => l.TriggerId == RejectedTriggerId)).IsEqualTo(1);
        await Assert.That(Db.NotificationLogs.Count(l => l.TriggerId == ApprovedTriggerId)).IsEqualTo(0);
    }

    [Test]
    public async Task Federal_dialog_approval_closes_federal_motion_and_releases_note() {
        var applicationId = await SubmitMatchingApplicationAsync();
        await RunJobAsync(DateTime.UtcNow);
        await ProcessAsync(_stateProcessorToken, applicationId, ApplicationStatus.Approved);
        await RunJobAsync(DateTime.UtcNow.AddMinutes(1));

        await ProcessAsync(_rootProcessorToken, applicationId, ApplicationStatus.Approved);
        await RunJobAsync(DateTime.UtcNow.AddMinutes(2));

        var issue = FakeRedmineClient.Issues.Single();
        await Assert.That(FederalMotion(applicationId)!.ApprovalStatus).IsEqualTo(MotionApprovalStatus.ClosedWithoutAction);
        await Assert.That(issue.Notes.Single()).Contains("angenommen");
        await Assert.That(issue.Notes.Single()).Contains("Vorstand Bund");

        issue.StatusId = SageSetupSeeder.SageDoneStatusId;
        await RunJobAsync(DateTime.UtcNow.AddDays(2));

        await Assert.That(StepOf(applicationId, ForwardingStepKind.AwaitSageEntry).State).IsEqualTo(ForwardingStepState.Done);
        await Assert.That(Db.NotificationLogs.Count(l => l.TriggerId == ApprovedTriggerId && l.Recipient == "bea@test.local")).IsEqualTo(1);
    }

    [Test]
    public async Task Chapter_dialog_approval_closes_only_chapter_motion() {
        var applicationId = await SubmitMatchingApplicationAsync();

        await ProcessAsync(_stateProcessorToken, applicationId, ApplicationStatus.Approved);

        await Assert.That(ChapterMotion(applicationId).ApprovalStatus).IsEqualTo(MotionApprovalStatus.ClosedWithoutAction);
        await Assert.That(FederalMotion(applicationId)!.ApprovalStatus).IsEqualTo(MotionApprovalStatus.Pending);
        await Assert.That(Db.MembershipApplications.Single(a => a.Id == applicationId).Status).IsEqualTo(ApplicationStatus.PendingFederalApproval);
    }

    [Test]
    public async Task Late_vote_keeps_chapter_dialog_rejection() {
        var applicationId = await SubmitMatchingApplicationAsync();
        await ProcessAsync(_stateProcessorToken, applicationId, ApplicationStatus.Rejected);

        var vote = await TryVoteAsync(ChapterMotion(applicationId).Id, _stateOfficer, VoteType.Approve);

        await Assert.That(vote).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Db.MembershipApplications.Single(a => a.Id == applicationId).Status).IsEqualTo(ApplicationStatus.Rejected);
        await Assert.That(FederalMotion(applicationId)).IsNull();
        await Assert.That(Db.ApplicantRegisterEntries.Count(e => e.SourceApplicationId == applicationId)).IsEqualTo(1);
        await Assert.That(Db.NotificationLogs.Count(l => l.TriggerId == ApprovedTriggerId)).IsEqualTo(0);
    }

    [Test]
    public async Task Late_vote_keeps_federal_dialog_approval() {
        var applicationId = await SubmitMatchingApplicationAsync();
        await ProcessAsync(_stateProcessorToken, applicationId, ApplicationStatus.Approved);
        await ProcessAsync(_rootProcessorToken, applicationId, ApplicationStatus.Approved);

        var vote = await TryVoteAsync(FederalMotion(applicationId)!.Id, _rootOfficer, VoteType.Deny);

        await Assert.That(vote).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Db.MembershipApplications.Single(a => a.Id == applicationId).Status).IsEqualTo(ApplicationStatus.Approved);
        await Assert.That(AutomaticEntry(applicationId)).IsNull();
        await Assert.That(Db.NotificationLogs.Count(l => l.TriggerId == RejectedTriggerId)).IsEqualTo(0);
    }

    [Test]
    public async Task Rejection_adds_register_entry_by_chapter_motion() {
        var applicationId = await SubmitMatchingApplicationAsync();

        await VoteAsync(ChapterMotion(applicationId).Id, _stateOfficer, VoteType.Deny);

        var entry = AutomaticEntry(applicationId);
        await Assert.That(Db.MembershipApplications.Single(a => a.Id == applicationId).Status).IsEqualTo(ApplicationStatus.Rejected);
        await Assert.That(FederalMotion(applicationId)).IsNull();
        await Assert.That(entry).IsNotNull();
        await Assert.That(entry!.Kind).IsEqualTo(ApplicantRegisterKind.Rejected);
        await Assert.That(entry.ChapterId).IsEqualTo(_state.Id);
        await Assert.That(entry.FirstName).IsEqualTo("Bea");
        await Assert.That(entry.LastName).IsEqualTo("Neu");
        await Assert.That(entry.DateOfBirth).IsEqualTo(new DateTime(1995, 3, 3));
        await Assert.That(entry.Street).IsEqualTo("Weg");
        await Assert.That(entry.HouseNumber).IsEqualTo("1");
        await Assert.That(entry.PostCode).IsEqualTo("30159");
        await Assert.That(entry.City).IsEqualTo("Hannover");
        await Assert.That(entry.Email).IsEqualTo("bea@test.local");
        await Assert.That(entry.OccurredOn.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)).IsEqualTo(Today());
        await Assert.That(entry.CreatedByUserId).IsNull();
    }

    [Test]
    public async Task Rejection_adds_register_entry_by_federal_motion() {
        var applicationId = await SubmitMatchingApplicationAsync();
        await VoteAsync(ChapterMotion(applicationId).Id, _stateOfficer, VoteType.Approve);

        await VoteAsync(FederalMotion(applicationId)!.Id, _rootOfficer, VoteType.Deny);

        await Assert.That(Db.MembershipApplications.Single(a => a.Id == applicationId).Status).IsEqualTo(ApplicationStatus.Rejected);
        await Assert.That(Db.ApplicantRegisterEntries.Count(e => e.SourceApplicationId == applicationId)).IsEqualTo(1);
        await Assert.That(AutomaticEntry(applicationId)!.ChapterId).IsEqualTo(_root.Id);
    }

    [Test]
    public async Task Rejection_adds_register_entry_by_chapter_dialog() {
        var applicationId = await SubmitMatchingApplicationAsync();

        var status = await ProcessAsync(_stateProcessorToken, applicationId, ApplicationStatus.Rejected);

        await Assert.That(status).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(FederalMotion(applicationId)).IsNull();
        await Assert.That(AutomaticEntry(applicationId)!.ChapterId).IsEqualTo(_state.Id);
    }

    [Test]
    public async Task Rejection_adds_register_entry_by_federal_dialog() {
        var applicationId = await SubmitMatchingApplicationAsync();
        await ProcessAsync(_stateProcessorToken, applicationId, ApplicationStatus.Approved);

        var status = await ProcessAsync(_rootProcessorToken, applicationId, ApplicationStatus.Rejected);

        await Assert.That(status).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Db.ApplicantRegisterEntries.Count(e => e.SourceApplicationId == applicationId)).IsEqualTo(1);
        await Assert.That(AutomaticEntry(applicationId)!.ChapterId).IsEqualTo(_root.Id);
    }

    [Test]
    public async Task Formal_rejection_adds_no_entry() {
        var applicationId = await SubmitMatchingApplicationAsync();
        var chapterMotionId = ChapterMotion(applicationId).Id;

        using (var scope = Factory.Services.CreateScope()) {
            scope.ServiceProvider.GetRequiredService<MotionRepository>().UpdateApprovalStatus(chapterMotionId, MotionApprovalStatus.FormallyRejected);
        }
        await WithFlowAsync(flow => flow.OnMotionResolvedAsync(chapterMotionId));

        await Assert.That(Db.MembershipApplications.Single(a => a.Id == applicationId).Status).IsEqualTo(ApplicationStatus.Rejected);
        await Assert.That(AutomaticEntry(applicationId)).IsNull();
    }

    [Test]
    public async Task Closed_without_action_adds_no_entry() {
        var applicationId = await SubmitMatchingApplicationAsync();
        var chapterMotionId = ChapterMotion(applicationId).Id;

        using (var scope = Factory.Services.CreateScope()) {
            scope.ServiceProvider.GetRequiredService<MotionRepository>().UpdateApprovalStatus(chapterMotionId, MotionApprovalStatus.ClosedWithoutAction);
        }
        await WithFlowAsync(flow => flow.OnMotionResolvedAsync(chapterMotionId));

        var application = Db.MembershipApplications.Single(a => a.Id == applicationId);
        await Assert.That(application.Status).IsEqualTo(ApplicationStatus.Pending);
        await Assert.That(application.FederalApprovalMotionId).IsNull();
        await Assert.That(FederalMotion(applicationId)).IsNull();
        await Assert.That(AutomaticEntry(applicationId)).IsNull();
    }

    [Test]
    public async Task Dialog_hearing_carries_decision_time() {
        var applicationId = await SubmitMatchingApplicationAsync();
        var decidedAtUtc = DateTime.UtcNow.AddDays(-2);
        using (var scope = Factory.Services.CreateScope()) {
            scope.ServiceProvider.GetRequiredService<MembershipApplicationRepository>()
                .ClaimStatus(applicationId, ApplicationStatus.Pending, ApplicationStatus.PendingFederalApproval, null);
        }

        await WithFlowAsync(flow => flow.OnRecordDecidedAsync(applicationId, decidedAtUtc));

        var federal = FederalMotion(applicationId)!;
        var date = BerlinTime.FromUtc(decidedAtUtc).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        await Assert.That(federal.TextMarkdown).Contains($"Anhörung: Gliederung Land hat am {date} im Bearbeitungsdialog zugestimmt");
        await Assert.That(federal.CreatedAt).IsEqualTo(decidedAtUtc).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task Detail_shows_federal_motion() {
        var applicationId = await SubmitMatchingApplicationAsync();
        await VoteAsync(ChapterMotion(applicationId).Id, _stateOfficer, VoteType.Approve);
        var (_, viewerToken) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [_state.Id] = new[] { PermissionIdentifier.ViewApplications } });

        using var viewer = AuthenticatedClient(viewerToken);
        var dto = await viewer.GetFromJsonAsync<MembershipApplicationDetailDTO>($"/api/admin/membershipapplications/{applicationId}");

        await Assert.That(dto!.Status).IsEqualTo(ApplicationStatus.PendingFederalApproval);
        await Assert.That(dto.FederalApprovalMotionId).IsEqualTo(FederalMotion(applicationId)!.Id);
    }

    [Test]
    public async Task Detail_hides_hits_without_permission() {
        var applicationId = await SubmitMatchingApplicationAsync();
        var (_, viewerToken) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [_state.Id] = new[] { PermissionIdentifier.ViewApplications } });
        var (_, holderToken) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewApplications, PermissionIdentifier.ManageApplicantRegister });

        using var viewer = AuthenticatedClient(viewerToken);
        var viewerResponse = await viewer.GetAsync($"/api/admin/membershipapplications/{applicationId}");
        var viewerJson = await viewerResponse.Content.ReadAsStringAsync();
        var viewerDto = await viewerResponse.Content.ReadFromJsonAsync<MembershipApplicationDetailDTO>();
        using var holder = AuthenticatedClient(holderToken);
        var holderDto = await holder.GetFromJsonAsync<MembershipApplicationDetailDTO>($"/api/admin/membershipapplications/{applicationId}");

        await Assert.That(viewerResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(viewerDto!.RequiresFederalApproval).IsTrue();
        await Assert.That(viewerDto.RegisterHits).IsNull();
        await Assert.That(viewerJson).DoesNotContain(_entry.Id.ToString());
        await Assert.That(holderDto!.RequiresFederalApproval).IsTrue();
        var hit = holderDto.RegisterHits!.Single();
        await Assert.That(hit.EntryId).IsEqualTo(_entry.Id);
        await Assert.That(hit.Kind).IsEqualTo(ApplicantRegisterKind.Rejected);
        await Assert.That(hit.OccurredOn).IsEqualTo(new DateOnly(2024, 6, 7));
        await Assert.That(hit.FirstName).IsEqualTo("Bea");
        await Assert.That(hit.LastName).IsEqualTo("Neu");
        await Assert.That(hit.MatchedFields.HasFlag(RegisterMatchFields.Email)).IsTrue();
        await Assert.That(hit.DismissedAt).IsNull();
    }
}
