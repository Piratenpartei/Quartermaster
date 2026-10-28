using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.Interceptors;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quartermaster.Api;
using Quartermaster.Api.ApplicantRegister;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.Forwarding;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Api.Motions;
using Quartermaster.Data;
using Quartermaster.Data.ApplicantRegister;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Forwarding;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Members;
using Quartermaster.Data.Motions;
using Quartermaster.Server.Forwarding;
using Quartermaster.Server.Forwarding.SageEntry;
using Quartermaster.Server.Tests.Forwarding.Fakes;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.ApplicantRegister;

[NotInParallel("FakeRedmineClient")]
public class RegisterHitDismissTests : IntegrationTestBase {
    private const string FederalApprovalNotice = "Benötigt zusätzlich die Zustimmung des Bundesvorstands (§ 3 Abs. 2 BS).";

    private Chapter _root = default!;
    private Chapter _state = default!;
    private Member _rootOfficer = default!;
    private Member _stateOfficer = default!;
    private Guid _voterId;
    private string _voterToken = "";
    private string _managerToken = "";
    private string _stateProcessorToken = "";

    public RegisterHitDismissTests() {
        DbContext.SupplementDefaults(Factory.Services);
        FakeRedmineClient.Reset();
    }

    private static MembershipApplicationDTO Dto(Guid divisionId, bool priorDeclined) => new() {
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
        ConformityDeclarationAccepted = true,
        HasPriorDeclinedApplication = priorDeclined,
        EntryDate = DateOnly.FromDateTime(DateTime.UtcNow)
    };

    private async Task<Guid> SubmitMatchingApplicationAsync(bool priorDeclined = false) {
        _root = Builder.SeedChapter("Bund");
        _state = Builder.SeedChapter("Land", parentChapterId: _root.Id);
        SageSetupSeeder.Seed(Factory.Services, _root.Id, _state.Id);
        Builder.SeedRegisterEntry(
            chapterId: _root.Id,
            firstName: "Bea",
            lastName: "Neu",
            dateOfBirth: new DateTime(1995, 3, 3),
            street: "Anderswo",
            postCode: "99999",
            city: "Woanders",
            email: "bea@test.local",
            occurredOn: new DateTime(2024, 6, 7));

        _rootOfficer = Builder.SeedMember(_root.Id, firstName: "Bundes", lastName: "Vorstand");
        Builder.SeedChapterOfficer(_rootOfficer.Id, _root.Id);
        _stateOfficer = Builder.SeedMember(_state.Id, firstName: "Landes", lastName: "Vorstand");
        Builder.SeedChapterOfficer(_stateOfficer.Id, _state.Id);
        (var voter, _voterToken) = Builder.SeedAuthenticatedUser(globalPermissions: new[] { PermissionIdentifier.SystemVote });
        _voterId = voter.Id;
        (_, _managerToken) = Builder.SeedAuthenticatedUser(globalPermissions: new[] { PermissionIdentifier.ManageApplicantRegister });
        (_, _stateProcessorToken) = Builder.SeedAuthenticatedUser(
            email: "land@test.local",
            chapterPermissions: new() { [_state.Id] = new[] { PermissionIdentifier.ProcessApplications } });

        var (_, submitterToken) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(submitterToken);
        var response = await client.PostAsJsonAsync("/api/membershipapplications", Dto(Builder.SeedDivisionFor(_state.Id), priorDeclined));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        return Db.MembershipApplications.Single().Id;
    }

    private Motion ChapterMotion(Guid applicationId) {
        return Db.Motions.Single(m => m.LinkedMembershipApplicationId == applicationId && m.ChapterId == _state.Id);
    }

    private Motion FederalMotion(Guid applicationId) {
        return Db.Motions.Single(m => m.LinkedMembershipApplicationId == applicationId && m.ChapterId == _root.Id);
    }

    private async Task VoteAsync(Guid motionId, Member officer, VoteType vote) {
        using var client = await AuthenticatedClientWithCsrfAsync(_voterToken);
        var response = await client.PostAsJsonAsync("/api/motions/vote", new MotionVoteRequest { MotionId = motionId, MemberId = officer.Id, Vote = vote });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    private async Task ApproveByChapterAsync(Guid applicationId) {
        await VoteAsync(ChapterMotion(applicationId).Id, _stateOfficer, VoteType.Approve);
    }

    private async Task ApproveByChapterDialogAsync(Guid applicationId) {
        using var client = await AuthenticatedClientWithCsrfAsync(_stateProcessorToken);
        var response = await client.PostAsJsonAsync("/api/admin/membershipapplications/process", new { Id = applicationId, Status = ApplicationStatus.Approved });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    private async Task<HttpStatusCode> DismissAsync(string token, Guid applicationId, Guid hitId) {
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync($"/api/admin/membershipapplications/{applicationId}/register-hits/{hitId}/dismiss", new { });
        return response.StatusCode;
    }

    private MembershipApplication Application(Guid applicationId) {
        return Db.MembershipApplications.Single(a => a.Id == applicationId);
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

    private int ApprovalMails() {
        return Db.NotificationLogs.Count(l => l.TriggerId == "application_approved" && l.Recipient == "bea@test.local");
    }

    private int RejectionMails() {
        return Db.NotificationLogs.Count(l => l.TriggerId == "application_rejected" && l.Recipient == "bea@test.local");
    }

    private ForwardingStep StepOf(Guid applicationId, ForwardingStepKind kind) {
        var forwardingCase = Db.ForwardingCases.Single(c => c.Kind == ForwardingCaseKind.Application && c.MembershipApplicationId == applicationId);
        return Db.ForwardingSteps.Single(s => s.CaseId == forwardingCase.Id && s.Kind == kind);
    }

    /// <summary>A host whose DbContexts carry <paramref name="interceptor"/>.</summary>
    private WebApplicationFactory<Program> HostWith(CommandInterceptor interceptor) {
        return Factory.WithWebHostBuilder(b => b.ConfigureServices(services => {
            services.RemoveAll<DbContext>();
            services.AddScoped(_ => {
                var context = new DbContext(new DataOptions().UseMySqlConnector(Database.ConnectionString));
                context.AddInterceptor(interceptor);
                return context;
            });
        }));
    }

    [Test]
    public async Task Dismiss_before_chapter_decision_makes_it_normal() {
        var applicationId = await SubmitMatchingApplicationAsync();
        var hit = Db.ApplicationRegisterHits.Single();

        var status = await DismissAsync(_managerToken, applicationId, hit.Id);
        await ApproveByChapterAsync(applicationId);

        await Assert.That(status).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Db.ApplicationRegisterHits.Single().DismissedAt).IsNotNull();
        await Assert.That(Db.ApplicationRegisterHits.Single().DismissedByUserId).IsNotNull();
        await Assert.That(Application(applicationId).RequiresFederalApproval).IsFalse();
        await Assert.That(Application(applicationId).Status).IsEqualTo(ApplicationStatus.Approved);
        await Assert.That(Db.Motions.Count(m => m.LinkedMembershipApplicationId == applicationId && m.ChapterId == _root.Id)).IsEqualTo(0);
    }

    [Test]
    public async Task Dismiss_after_chapter_approval_approves() {
        var applicationId = await SubmitMatchingApplicationAsync();
        await ApproveByChapterAsync(applicationId);
        var hit = Db.ApplicationRegisterHits.Single();
        await Assert.That(Application(applicationId).Status).IsEqualTo(ApplicationStatus.PendingFederalApproval);

        var status = await DismissAsync(_managerToken, applicationId, hit.Id);

        var application = Application(applicationId);
        var forwardingCase = Db.ForwardingCases.Single(c => c.MembershipApplicationId == applicationId);
        await Assert.That(status).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(application.Status).IsEqualTo(ApplicationStatus.Approved);
        await Assert.That(application.ProcessedAt).IsNotNull();
        await Assert.That(application.RequiresFederalApproval).IsFalse();
        await Assert.That(FederalMotion(applicationId).ApprovalStatus).IsEqualTo(MotionApprovalStatus.ClosedWithoutAction);
        await Assert.That(forwardingCase.MotionId).IsEqualTo(ChapterMotion(applicationId).Id);
        await Assert.That(forwardingCase.DecidingChapterId).IsEqualTo(_state.Id);
        var closure = Db.AuditLogs.Single(e => e.EntityId == FederalMotion(applicationId).Id && e.Action == "ClosedWithoutAction");
        await Assert.That(closure.NewValue).IsEqualTo("Register hits dismissed as another person");
        await Assert.That(StepOf(applicationId, ForwardingStepKind.AddOutcomeNote).State).IsNotEqualTo(ForwardingStepState.Waiting);
        await Assert.That(StepOf(applicationId, ForwardingStepKind.AwaitSageEntry).State).IsNotEqualTo(ForwardingStepState.Waiting);
    }

    [Test]
    public async Task Dismiss_after_dialog_approval_approves_on_chapter_date() {
        var applicationId = await SubmitMatchingApplicationAsync();
        await RunJobAsync(DateTime.UtcNow);
        await ApproveByChapterDialogAsync(applicationId);
        await RunJobAsync(DateTime.UtcNow.AddMinutes(1));
        var chapterDecidedAt = DateTime.UtcNow.AddDays(-3);
        Db.Motions.Where(m => m.Id == FederalMotion(applicationId).Id).Set(m => m.CreatedAt, chapterDecidedAt).Update();
        var hit = Db.ApplicationRegisterHits.Single();
        await Assert.That(StepOf(applicationId, ForwardingStepKind.AddOutcomeNote).State).IsEqualTo(ForwardingStepState.Waiting);

        var status = await DismissAsync(_managerToken, applicationId, hit.Id);
        var released = StepOf(applicationId, ForwardingStepKind.AddOutcomeNote).State;
        await RunJobAsync(DateTime.UtcNow.AddMinutes(2));

        var application = Application(applicationId);
        var forwardingCase = Db.ForwardingCases.Single(c => c.MembershipApplicationId == applicationId);
        var chapterDate = BerlinTime.FromUtc(chapterDecidedAt).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        await Assert.That(status).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(application.Status).IsEqualTo(ApplicationStatus.Approved);
        await Assert.That(application.RequiresFederalApproval).IsFalse();
        await Assert.That(FederalMotion(applicationId).ApprovalStatus).IsEqualTo(MotionApprovalStatus.ClosedWithoutAction);
        await Assert.That(forwardingCase.MotionId).IsEqualTo(ChapterMotion(applicationId).Id);
        await Assert.That(forwardingCase.DecidingChapterId).IsEqualTo(_state.Id);
        await Assert.That(released).IsNotEqualTo(ForwardingStepState.Waiting);
        var note = FakeRedmineClient.Issues.Single().Notes.Single();
        await Assert.That(note).Contains("Vorstand Land");
        await Assert.That(note).Contains(chapterDate);
    }

    [Test]
    public async Task Failed_approval_keeps_hit_open() {
        var applicationId = await SubmitMatchingApplicationAsync();
        await ApproveByChapterAsync(applicationId);
        var hit = Db.ApplicationRegisterHits.Single();
        var interceptor = new BeforeUpdateInterceptor(MembershipApplication.TableName, () => throw new InvalidOperationException("Simulated failure"));
        using var host = HostWith(interceptor);
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _managerToken);
        await AttachAntiforgeryTokenAsync(client);

        var response = await client.PostAsJsonAsync($"/api/admin/membershipapplications/{applicationId}/register-hits/{hit.Id}/dismiss", new { });

        var application = Application(applicationId);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.InternalServerError);
        await Assert.That(Db.ApplicationRegisterHits.Single().DismissedAt).IsNull();
        await Assert.That(application.Status).IsEqualTo(ApplicationStatus.PendingFederalApproval);
        await Assert.That(application.RequiresFederalApproval).IsTrue();
        await Assert.That(FederalMotion(applicationId).ApprovalStatus).IsEqualTo(MotionApprovalStatus.Pending);
    }

    [Test]
    public async Task Self_declared_pending_federal_keeps_requirement() {
        var applicationId = await SubmitMatchingApplicationAsync(priorDeclined: true);
        await ApproveByChapterAsync(applicationId);
        var hit = Db.ApplicationRegisterHits.Single();

        var status = await DismissAsync(_managerToken, applicationId, hit.Id);

        var application = Application(applicationId);
        var forwardingCase = Db.ForwardingCases.Single(c => c.MembershipApplicationId == applicationId);
        await Assert.That(status).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Db.ApplicationRegisterHits.Single().DismissedAt).IsNotNull();
        await Assert.That(application.Status).IsEqualTo(ApplicationStatus.PendingFederalApproval);
        await Assert.That(application.RequiresFederalApproval).IsTrue();
        await Assert.That(FederalMotion(applicationId).ApprovalStatus).IsEqualTo(MotionApprovalStatus.Pending);
        await Assert.That(forwardingCase.MotionId).IsEqualTo(FederalMotion(applicationId).Id);
        await Assert.That(ApprovalMails()).IsEqualTo(0);
    }

    [Test]
    public async Task Dismiss_before_chapter_decision_removes_notice_and_keeps_votes() {
        var applicationId = await SubmitMatchingApplicationAsync();
        var hit = Db.ApplicationRegisterHits.Single();
        var motionId = ChapterMotion(applicationId).Id;
        var noticeBefore = ChapterMotion(applicationId).TextMarkdown.Contains(FederalApprovalNotice);
        Db.Insert(new MotionVote { MotionId = motionId, MemberId = _stateOfficer.Id, CastByUserId = _voterId, Vote = VoteType.Abstain, VotedAt = DateTime.UtcNow });

        var status = await DismissAsync(_managerToken, applicationId, hit.Id);

        var motion = ChapterMotion(applicationId);
        await Assert.That(status).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(noticeBefore).IsTrue();
        await Assert.That(motion.TextMarkdown).DoesNotContain(FederalApprovalNotice);
        await Assert.That(motion.Text).DoesNotContain("Zustimmung des Bundesvorstands");
        await Assert.That(motion.TextMarkdown).Contains($"/Administration/MembershipApplications/{applicationId}");
        await Assert.That(motion.ApprovalStatus).IsEqualTo(MotionApprovalStatus.Pending);
        await Assert.That(Db.MotionVotes.Count(v => v.MotionId == motionId)).IsEqualTo(1);
        await Assert.That(Db.AuditLogs.Count(e => e.EntityId == motionId && e.FieldName == nameof(Motion.TextMarkdown))).IsEqualTo(1);
    }

    [Test]
    public async Task Dismiss_removes_notice_edited_in_whitespace_and_case() {
        var applicationId = await SubmitMatchingApplicationAsync();
        var hit = Db.ApplicationRegisterHits.Single();
        var motionId = ChapterMotion(applicationId).Id;
        var edited = ChapterMotion(applicationId).TextMarkdown
            .Replace("\n", "\r\n")
            .Replace(FederalApprovalNotice, "  benötigt  zusätzlich die Zustimmung des Bundesvorstands (§3 Abs. 2 BS). ")
            + "\r\nErgänzt vom Vorstand.\r\n";
        Db.Motions.Where(m => m.Id == motionId).Set(m => m.TextMarkdown, edited).Update();

        var status = await DismissAsync(_managerToken, applicationId, hit.Id);

        var motion = ChapterMotion(applicationId);
        await Assert.That(status).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(motion.TextMarkdown).DoesNotContain("Zustimmung des Bundesvorstands");
        await Assert.That(motion.Text).DoesNotContain("Zustimmung des Bundesvorstands");
        await Assert.That(motion.TextMarkdown).Contains("Ergänzt vom Vorstand.");
        await Assert.That(motion.TextMarkdown).Contains($"/Administration/MembershipApplications/{applicationId}");
    }

    [Test]
    public async Task Dismiss_after_chapter_approval_mails_once_after_sage() {
        var applicationId = await SubmitMatchingApplicationAsync();
        await RunJobAsync(DateTime.UtcNow);
        await ApproveByChapterAsync(applicationId);
        await RunJobAsync(DateTime.UtcNow.AddMinutes(1));
        var hit = Db.ApplicationRegisterHits.Single();

        await DismissAsync(_managerToken, applicationId, hit.Id);
        await RunJobAsync(DateTime.UtcNow.AddMinutes(2));
        var mailsBeforeSage = ApprovalMails();
        FakeRedmineClient.Issues.Single().StatusId = SageSetupSeeder.SageDoneStatusId;
        await RunJobAsync(DateTime.UtcNow.AddDays(2));
        await RunJobAsync(DateTime.UtcNow.AddDays(3));

        await Assert.That(Application(applicationId).Status).IsEqualTo(ApplicationStatus.Approved);
        await Assert.That(mailsBeforeSage).IsEqualTo(0);
        await Assert.That(ApprovalMails()).IsEqualTo(1);
    }

    [Test]
    public async Task Self_declared_stays_federal_after_dismiss() {
        var applicationId = await SubmitMatchingApplicationAsync(priorDeclined: true);
        var hit = Db.ApplicationRegisterHits.Single();

        var status = await DismissAsync(_managerToken, applicationId, hit.Id);
        await ApproveByChapterAsync(applicationId);

        await Assert.That(status).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Application(applicationId).RequiresFederalApproval).IsTrue();
        await Assert.That(Application(applicationId).Status).IsEqualTo(ApplicationStatus.PendingFederalApproval);
    }

    [Test]
    public async Task Two_hits_need_both_dismissed() {
        var applicationId = await SubmitMatchingApplicationAsync();
        var secondEntry = Builder.SeedRegisterEntry(chapterId: _root.Id, firstName: "Bea", lastName: "Neu", email: "bea@test.local");
        Db.Insert(new ApplicationRegisterHit {
            ApplicationId = applicationId,
            EntryId = secondEntry.Id,
            MatchedFields = RegisterMatchFields.Email,
            CreatedAt = DateTime.UtcNow
        });
        await ApproveByChapterAsync(applicationId);
        var hits = Db.ApplicationRegisterHits.OrderBy(h => h.CreatedAt).ToList();

        var first = await DismissAsync(_managerToken, applicationId, hits[0].Id);
        var afterFirst = Application(applicationId);
        var second = await DismissAsync(_managerToken, applicationId, hits[1].Id);

        await Assert.That(first).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(afterFirst.RequiresFederalApproval).IsTrue();
        await Assert.That(afterFirst.Status).IsEqualTo(ApplicationStatus.PendingFederalApproval);
        await Assert.That(second).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Application(applicationId).RequiresFederalApproval).IsFalse();
        await Assert.That(Application(applicationId).Status).IsEqualTo(ApplicationStatus.Approved);
    }

    [Test]
    public async Task Concurrent_dismissals_of_both_hits_lift_the_requirement() {
        var applicationId = await SubmitMatchingApplicationAsync();
        var secondEntry = Builder.SeedRegisterEntry(chapterId: _root.Id, firstName: "Bea", lastName: "Neu", email: "bea@test.local");
        Db.Insert(new ApplicationRegisterHit {
            ApplicationId = applicationId,
            EntryId = secondEntry.Id,
            MatchedFields = RegisterMatchFields.Email,
            CreatedAt = DateTime.UtcNow
        });
        await ApproveByChapterAsync(applicationId);
        var hits = Db.ApplicationRegisterHits.OrderBy(h => h.CreatedAt).ToList();
        WebApplicationFactory<Program>? host = null;
        Task<HttpStatusCode>? concurrent = null;
        var interceptor = new BeforeExistsQueryInterceptor(ApplicationRegisterHit.TableName, () => {
            concurrent = Task.Run(() => DismissThroughAsync(host!, applicationId, hits[1].Id));
            WaitUntilDoneOrBlocked(concurrent);
        });
        host = HostWith(interceptor);

        HttpStatusCode first;
        HttpStatusCode second;
        try {
            first = await DismissThroughAsync(host, applicationId, hits[0].Id);
            second = await concurrent!;
        } finally {
            host.Dispose();
        }

        var application = Application(applicationId);
        await Assert.That(first).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(second).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Db.ApplicationRegisterHits.Count(h => h.DismissedAt == null)).IsEqualTo(0);
        await Assert.That(application.RequiresFederalApproval).IsFalse();
        await Assert.That(application.Status).IsEqualTo(ApplicationStatus.Approved);
    }

    private async Task<HttpStatusCode> DismissThroughAsync(WebApplicationFactory<Program> host, Guid applicationId, Guid hitId) {
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _managerToken);
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsJsonAsync($"/api/admin/membershipapplications/{applicationId}/register-hits/{hitId}/dismiss", new { });
        return response.StatusCode;
    }

    /// <summary>Returns once <paramref name="request"/> finished or waits for the application row lock held by the transaction that started it.</summary>
    private void WaitUntilDoneOrBlocked(Task request) {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!request.IsCompleted && !WaitsForApplicationLock()) {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("The concurrent dismissal neither finished nor waited for a lock.");
            Thread.Sleep(20);
        }
    }

    /// <summary>MariaDB's INNODB_TRX leaves out a transaction waiting in a locking read, so this looks for the statement itself.</summary>
    private bool WaitsForApplicationLock() {
        return Db.Query<int>(
            "SELECT COUNT(*) FROM information_schema.PROCESSLIST "
            + $"WHERE DB = DATABASE() AND COMMAND = 'Query' AND INFO LIKE '%FROM {MembershipApplication.TableName} WHERE Id = % FOR UPDATE'").Single() > 0;
    }

    [Test]
    public async Task Dismiss_needs_permission() {
        var applicationId = await SubmitMatchingApplicationAsync();
        var hit = Db.ApplicationRegisterHits.Single();
        var (_, otherToken) = Builder.SeedAuthenticatedUser(globalPermissions: new[] { PermissionIdentifier.ViewApplications });

        var status = await DismissAsync(otherToken, applicationId, hit.Id);

        await Assert.That(status).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(Db.ApplicationRegisterHits.Single().DismissedAt).IsNull();
        await Assert.That(Application(applicationId).RequiresFederalApproval).IsTrue();
    }

    [Test]
    public async Task Dismiss_twice_conflicts() {
        var applicationId = await SubmitMatchingApplicationAsync();
        var hit = Db.ApplicationRegisterHits.Single();

        var first = await DismissAsync(_managerToken, applicationId, hit.Id);
        var second = await DismissAsync(_managerToken, applicationId, hit.Id);

        await Assert.That(first).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(second).IsEqualTo(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Unknown_or_foreign_hit_is_not_found() {
        var applicationId = await SubmitMatchingApplicationAsync();
        var hit = Db.ApplicationRegisterHits.Single();
        var otherApplication = Builder.SeedMembershipApplication();

        var unknown = await DismissAsync(_managerToken, applicationId, Guid.NewGuid());
        var foreign = await DismissAsync(_managerToken, otherApplication.Id, hit.Id);

        await Assert.That(unknown).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(foreign).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Dismiss_after_federal_rejection_only_marks_hit() {
        var applicationId = await SubmitMatchingApplicationAsync();
        await ApproveByChapterAsync(applicationId);
        await VoteAsync(FederalMotion(applicationId).Id, _rootOfficer, VoteType.Deny);
        var hit = Db.ApplicationRegisterHits.Single();
        var rejectionMails = RejectionMails();
        var caseBefore = Db.ForwardingCases.Single(c => c.MembershipApplicationId == applicationId);

        var status = await DismissAsync(_managerToken, applicationId, hit.Id);

        var application = Application(applicationId);
        var caseAfter = Db.ForwardingCases.Single(c => c.MembershipApplicationId == applicationId);
        await Assert.That(status).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Db.ApplicationRegisterHits.Single().DismissedAt).IsNotNull();
        await Assert.That(application.Status).IsEqualTo(ApplicationStatus.Rejected);
        await Assert.That(application.RequiresFederalApproval).IsTrue();
        await Assert.That(FederalMotion(applicationId).ApprovalStatus).IsEqualTo(MotionApprovalStatus.Rejected);
        await Assert.That(caseAfter.MotionId).IsEqualTo(caseBefore.MotionId);
        await Assert.That(caseAfter.DecidingChapterId).IsEqualTo(caseBefore.DecidingChapterId);
        await Assert.That(RejectionMails()).IsEqualTo(rejectionMails);
        await Assert.That(ApprovalMails()).IsEqualTo(0);
    }
}
