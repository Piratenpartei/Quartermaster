using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Microsoft.Extensions.DependencyInjection;
using Quartermaster.Api;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.Forwarding;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Api.Motions;
using Quartermaster.Data;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Forwarding;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Motions;
using Quartermaster.Data.Options;
using Quartermaster.Server.Chapters;
using Quartermaster.Server.Tests.Forwarding.Fakes;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Chapters;

[NotInParallel("FakeRedmineClient")]
public class MotionHandoverTests : IntegrationTestBase {
    private const string SubmittedTriggerId = "application_submitted";
    private const string ApprovedTriggerId = "application_approved";
    private const string RejectedTriggerId = "application_rejected";

    public MotionHandoverTests() {
        DbContext.SupplementDefaults(Factory.Services);
        FakeRedmineClient.Reset();
    }

    /// <summary>Stores the marks and hands over in one transaction, then sends the notifications, as the marks endpoint does.</summary>
    private int MarkAndHandOver(Guid chapterId, bool noMemberAdministration, bool unableToAct) {
        using var scope = Factory.Services.CreateScope();
        var provider = scope.ServiceProvider;
        var handover = provider.GetRequiredService<MotionHandover>();
        int count;
        using (var tx = provider.GetRequiredService<DbContext>().BeginTransaction()) {
            provider.GetRequiredService<ChapterRepository>().SetMarks(chapterId, noMemberAdministration, unableToAct);
            count = handover.HandOver(chapterId);
            tx.Commit();
        }
        handover.SendNotifications();
        return count;
    }

    private Motion SeedMotion(Guid chapterId, Guid? applicationId = null, Guid? dueSelectionId = null, bool isPublic = false) {
        var motion = Motion.Create(chapterId, "Anna Pirat", "anna@test.local", "Antrag von Anna", "Text **fett**", "<p>Text <strong>fett</strong></p>",
            DateTime.UtcNow.AddDays(-2), applicationId, dueSelectionId);
        motion.IsPublic = isPublic;
        Db.Insert(motion);
        return motion;
    }

    private Motion Reload(Guid motionId) {
        return Db.Motions.Single(m => m.Id == motionId);
    }

    private Motion Replacement(Motion old) {
        var reloaded = Reload(old.Id);
        return Reload(reloaded.HandedOverToMotionId!.Value);
    }

    private void EnableApplicationForwarding() {
        using var scope = Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<OptionRepository>().SetValue(ForwardingOptionKeys.MembershipApplicationEnabled, null, "true");
    }

    private void SetApplicationMode(Guid chapterId, ForwardingMode mode) {
        using var scope = Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ForwardingSettingsRepository>()
            .SaveChapter(new ForwardingChapterSetting { ChapterId = chapterId, ApplicationMode = mode }, []);
    }

    [Test]
    public async Task Application_motion_moves_to_parent() {
        var chain = Builder.SeedChapterHierarchy("Bund", "Land", "Kreis");
        var (land, kreis) = (chain[1], chain[2]);
        var application = Builder.SeedMembershipApplication(kreis.Id);
        var old = SeedMotion(kreis.Id, applicationId: application.Id);
        Builder.SeedAuthenticatedUser(email: "land@test.local", chapterPermissions: new() { [land.Id] = new[] { PermissionIdentifier.ProcessApplications } });

        var count = MarkAndHandOver(kreis.Id, noMemberAdministration: true, unableToAct: false);

        await Assert.That(count).IsEqualTo(1);
        var closed = Reload(old.Id);
        await Assert.That(closed.ApprovalStatus).IsEqualTo(MotionApprovalStatus.ClosedWithoutAction);
        await Assert.That(closed.HandedOverToMotionId).IsNotNull();
        var replacement = Replacement(old);
        await Assert.That(replacement.ChapterId).IsEqualTo(land.Id);
        await Assert.That(replacement.ApprovalStatus).IsEqualTo(MotionApprovalStatus.Pending);
        await Assert.That(replacement.Title).IsEqualTo(old.Title);
        await Assert.That(replacement.TextMarkdown).IsEqualTo(old.TextMarkdown);
        await Assert.That(replacement.Text).IsEqualTo(old.Text);
        await Assert.That(replacement.AuthorName).IsEqualTo(old.AuthorName);
        await Assert.That(replacement.AuthorEmail).IsEqualTo(old.AuthorEmail);
        await Assert.That(replacement.LinkedMembershipApplicationId).IsEqualTo(application.Id);
        await Assert.That(replacement.OnBehalfOfChapterId).IsEqualTo(kreis.Id);
        await Assert.That(replacement.OnBehalfOfReason).IsEqualTo(ChapterMark.NoMemberAdministration);
        await Assert.That(replacement.CreatedAt).IsGreaterThan(old.CreatedAt.AddDays(1));
        await Assert.That(Db.MembershipApplications.Single(a => a.Id == application.Id).ChapterId).IsEqualTo(land.Id);
        await Assert.That(Db.MembershipApplications.Single(a => a.Id == application.Id).Status).IsEqualTo(ApplicationStatus.Pending);
        await Assert.That(Db.AuditLogs.Count(e => e.EntityType == "Motion" && e.EntityId == old.Id && e.Action == "HandedOver"
            && e.NewValue == replacement.Id.ToString())).IsEqualTo(1);
        await Assert.That(Db.NotificationLogs.Count(l => l.TriggerId == SubmittedTriggerId && l.Recipient == "land@test.local")).IsEqualTo(1);
        await Assert.That(Db.NotificationLogs.Count(l => l.TriggerId == ApprovedTriggerId || l.TriggerId == RejectedTriggerId)).IsEqualTo(0);
    }

    [Test]
    public async Task Mark_handover_stores_reason() {
        var chain = Builder.SeedChapterHierarchy("Bund", "Land", "Kreis");
        var kreis = chain[2];
        var application = Builder.SeedMembershipApplication(kreis.Id);
        var old = SeedMotion(kreis.Id, applicationId: application.Id);

        MarkAndHandOver(kreis.Id, noMemberAdministration: true, unableToAct: false);

        await Assert.That(Reload(old.Id).HandoverReason).IsEqualTo(MotionHandoverReason.ChapterMark);
        await Assert.That(Reload(Replacement(old).Id).HandoverReason).IsNull();
    }

    [Test]
    public async Task Votes_do_not_carry_over() {
        var chain = Builder.SeedChapterHierarchy("Bund", "Land", "Kreis");
        var kreis = chain[2];
        var application = Builder.SeedMembershipApplication(kreis.Id);
        var old = SeedMotion(kreis.Id, applicationId: application.Id);
        var officer = Builder.SeedMember(kreis.Id);
        var (voter, _) = Builder.SeedAuthenticatedUser();
        Builder.SeedMotionVote(old.Id, officer.Id, voter.Id, VoteType.Approve);

        MarkAndHandOver(kreis.Id, noMemberAdministration: true, unableToAct: false);

        var replacement = Replacement(old);
        await Assert.That(Db.MotionVotes.Count(v => v.MotionId == replacement.Id)).IsEqualTo(0);
        await Assert.That(Db.MotionVotes.Count(v => v.MotionId == old.Id)).IsEqualTo(1);
    }

    [Test]
    public async Task Unlinked_motion_stays_without_unable_to_act() {
        var chain = Builder.SeedChapterHierarchy("Bund", "Land", "Kreis");
        var kreis = chain[2];
        var motion = SeedMotion(kreis.Id);

        var count = MarkAndHandOver(kreis.Id, noMemberAdministration: true, unableToAct: false);

        await Assert.That(count).IsEqualTo(0);
        var reloaded = Reload(motion.Id);
        await Assert.That(reloaded.ApprovalStatus).IsEqualTo(MotionApprovalStatus.Pending);
        await Assert.That(reloaded.ChapterId).IsEqualTo(kreis.Id);
        await Assert.That(reloaded.HandedOverToMotionId).IsNull();
        await Assert.That(Db.Motions.Count()).IsEqualTo(1);
    }

    [Test]
    public async Task Unlinked_motion_moves_when_unable_to_act() {
        var chain = Builder.SeedChapterHierarchy("Bund", "Land", "Kreis");
        var (land, kreis) = (chain[1], chain[2]);
        var old = SeedMotion(kreis.Id, isPublic: true);

        var count = MarkAndHandOver(kreis.Id, noMemberAdministration: false, unableToAct: true);

        await Assert.That(count).IsEqualTo(1);
        await Assert.That(Reload(old.Id).ApprovalStatus).IsEqualTo(MotionApprovalStatus.ClosedWithoutAction);
        var replacement = Replacement(old);
        await Assert.That(replacement.ChapterId).IsEqualTo(land.Id);
        await Assert.That(replacement.IsPublic).IsTrue();
        await Assert.That(replacement.OnBehalfOfChapterId).IsEqualTo(kreis.Id);
        await Assert.That(replacement.OnBehalfOfReason).IsEqualTo(ChapterMark.UnableToAct);
    }

    [Test]
    public async Task Reduction_motion_moves_to_federal() {
        var chain = Builder.SeedChapterHierarchy("Bund", "Land");
        var (bund, land) = (chain[0], chain[1]);
        var dueSelection = Builder.SeedDueSelection();
        Db.DueSelections.Where(d => d.Id == dueSelection.Id).Set(d => d.ChapterId, (Guid?)land.Id).Update();
        var old = SeedMotion(land.Id, dueSelectionId: dueSelection.Id);

        var count = MarkAndHandOver(land.Id, noMemberAdministration: true, unableToAct: false);

        await Assert.That(count).IsEqualTo(1);
        var replacement = Replacement(old);
        await Assert.That(replacement.ChapterId).IsEqualTo(bund.Id);
        await Assert.That(replacement.LinkedDueSelectionId).IsEqualTo(dueSelection.Id);
        var moved = Db.DueSelections.Single(d => d.Id == dueSelection.Id);
        await Assert.That(moved.ChapterId).IsEqualTo(bund.Id);
        await Assert.That(moved.Status).IsEqualTo(DueSelectionStatus.Pending);
    }

    [Test]
    public async Task Second_handover_keeps_original_chapter() {
        var chain = Builder.SeedChapterHierarchy("Bund", "Land", "Kreis", "Ort");
        var (land, kreis, ort) = (chain[1], chain[2], chain[3]);
        var application = Builder.SeedMembershipApplication(ort.Id);
        var old = SeedMotion(ort.Id, applicationId: application.Id);

        MarkAndHandOver(ort.Id, noMemberAdministration: true, unableToAct: false);
        var first = Replacement(old);
        MarkAndHandOver(kreis.Id, noMemberAdministration: false, unableToAct: true);

        await Assert.That(first.ChapterId).IsEqualTo(kreis.Id);
        var second = Replacement(first);
        await Assert.That(second.ChapterId).IsEqualTo(land.Id);
        await Assert.That(second.OnBehalfOfChapterId).IsEqualTo(ort.Id);
        await Assert.That(second.OnBehalfOfReason).IsEqualTo(ChapterMark.NoMemberAdministration);
        await Assert.That(Db.MembershipApplications.Single(a => a.Id == application.Id).ChapterId).IsEqualTo(land.Id);
    }

    [Test]
    public async Task Handed_over_motion_decides_application() {
        var chain = Builder.SeedChapterHierarchy("Bund", "Land", "Kreis");
        var (bund, land, kreis) = (chain[0], chain[1], chain[2]);
        SageSetupSeeder.Seed(Factory.Services, bund.Id, land.Id);
        SetApplicationMode(kreis.Id, ForwardingMode.Quartermaster);
        var landOfficer = Builder.SeedMember(land.Id, firstName: "Landes", lastName: "Vorstand");
        Builder.SeedChapterOfficer(landOfficer.Id, land.Id);
        var (_, voterToken) = Builder.SeedAuthenticatedUser(globalPermissions: new[] { PermissionIdentifier.SystemVote });

        var application = await SubmitApplicationAsync(kreis.Id);
        var old = Db.Motions.Single(m => m.LinkedMembershipApplicationId == application.Id);
        await Assert.That(old.ChapterId).IsEqualTo(kreis.Id);
        var forwardingCase = Db.ForwardingCases.Single(c => c.Kind == ForwardingCaseKind.Application && c.MembershipApplicationId == application.Id);
        await Assert.That(forwardingCase.MotionId).IsEqualTo(old.Id);

        MarkAndHandOver(kreis.Id, noMemberAdministration: true, unableToAct: false);

        var replacement = Replacement(old);
        await Assert.That(replacement.ChapterId).IsEqualTo(land.Id);
        var pointed = Db.ForwardingCases.Single(c => c.Id == forwardingCase.Id);
        await Assert.That(pointed.MotionId).IsEqualTo(replacement.Id);
        await Assert.That(pointed.DecidingChapterId).IsEqualTo(land.Id);
        var note = Db.ForwardingSteps.Single(s => s.CaseId == forwardingCase.Id && s.Kind == ForwardingStepKind.AddOutcomeNote);
        await Assert.That(note.State).IsEqualTo(ForwardingStepState.Waiting);
        await Assert.That(Db.MembershipApplications.Single(a => a.Id == application.Id).Status).IsEqualTo(ApplicationStatus.Pending);
        await Assert.That(Db.NotificationLogs.Count(l => l.TriggerId == ApprovedTriggerId || l.TriggerId == RejectedTriggerId)).IsEqualTo(0);

        await VoteAsync(voterToken, replacement.Id, landOfficer.Id);

        await Assert.That(Reload(replacement.Id).ApprovalStatus).IsEqualTo(MotionApprovalStatus.Approved);
        await Assert.That(Db.MembershipApplications.Single(a => a.Id == application.Id).Status).IsEqualTo(ApplicationStatus.Approved);
        await Assert.That(Db.ForwardingSteps.Single(s => s.Id == note.Id).State).IsEqualTo(ForwardingStepState.Pending);
        await Assert.That(Reload(old.Id).ApprovalStatus).IsEqualTo(MotionApprovalStatus.ClosedWithoutAction);
    }

    [Test]
    public async Task Detail_links_replacement_motion_after_handover() {
        var chain = Builder.SeedChapterHierarchy("Bund", "Land", "Kreis");
        var kreis = chain[2];
        var application = Builder.SeedMembershipApplication(kreis.Id);
        var applicationMotion = SeedMotion(kreis.Id, applicationId: application.Id);
        var dueSelection = Builder.SeedDueSelection();
        var reductionMotion = SeedMotion(kreis.Id, dueSelectionId: dueSelection.Id);

        MarkAndHandOver(kreis.Id, noMemberAdministration: true, unableToAct: false);

        var motions = new MotionRepository(Db, new AuditLogRepository(Db));
        await Assert.That(motions.GetByLinkedApplicationId(application.Id)!.Id).IsEqualTo(Replacement(applicationMotion).Id);
        await Assert.That(motions.GetByLinkedDueSelectionId(dueSelection.Id)!.Id).IsEqualTo(Replacement(reductionMotion).Id);
    }

    [Test]
    public async Task Dismissal_after_handover_points_case_at_replacement() {
        var chain = Builder.SeedChapterHierarchy("Bund", "Land", "Kreis");
        var (bund, land, kreis) = (chain[0], chain[1], chain[2]);
        SageSetupSeeder.Seed(Factory.Services, bund.Id, land.Id);
        SetApplicationMode(kreis.Id, ForwardingMode.Quartermaster);
        Builder.SeedRegisterEntry(chapterId: bund.Id, firstName: "Bea", lastName: "Neu", dateOfBirth: new DateTime(1995, 3, 3), email: "bea@test.local");
        var landOfficer = Builder.SeedMember(land.Id, firstName: "Landes", lastName: "Vorstand");
        Builder.SeedChapterOfficer(landOfficer.Id, land.Id);
        var (_, voterToken) = Builder.SeedAuthenticatedUser(globalPermissions: new[] { PermissionIdentifier.SystemVote });
        var (_, managerToken) = Builder.SeedAuthenticatedUser(globalPermissions: new[] { PermissionIdentifier.ManageApplicantRegister });
        var application = await SubmitApplicationAsync(kreis.Id);
        var old = Db.Motions.Single(m => m.LinkedMembershipApplicationId == application.Id);
        MarkAndHandOver(kreis.Id, noMemberAdministration: true, unableToAct: false);
        var replacement = Replacement(old);
        await VoteAsync(voterToken, replacement.Id, landOfficer.Id);
        await Assert.That(Db.MembershipApplications.Single(a => a.Id == application.Id).Status).IsEqualTo(ApplicationStatus.PendingFederalApproval);
        var hit = Db.ApplicationRegisterHits.Single();

        using (var client = await AuthenticatedClientWithCsrfAsync(managerToken)) {
            var dismiss = await client.PostAsJsonAsync($"/api/admin/membershipapplications/{application.Id}/register-hits/{hit.Id}/dismiss", new { });
            await Assert.That(dismiss.StatusCode).IsEqualTo(HttpStatusCode.OK);
        }

        await Assert.That(Db.MembershipApplications.Single(a => a.Id == application.Id).Status).IsEqualTo(ApplicationStatus.Approved);
        var forwardingCase = Db.ForwardingCases.Single(c => c.Kind == ForwardingCaseKind.Application && c.MembershipApplicationId == application.Id);
        await Assert.That(forwardingCase.MotionId).IsEqualTo(replacement.Id);
        await Assert.That(forwardingCase.DecidingChapterId).IsEqualTo(land.Id);
    }

    private async Task<MembershipApplication> SubmitApplicationAsync(Guid chapterId) {
        var (_, submitterToken) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(submitterToken);
        var submit = await client.PostAsJsonAsync("/api/membershipapplications", ApplicationDto(Builder.SeedDivisionFor(chapterId)));
        await Assert.That(submit.StatusCode).IsEqualTo(HttpStatusCode.OK);
        return Db.MembershipApplications.Single();
    }

    private async Task VoteAsync(string voterToken, Guid motionId, Guid officerId) {
        using var client = await AuthenticatedClientWithCsrfAsync(voterToken);
        var vote = await client.PostAsJsonAsync("/api/motions/vote", new MotionVoteRequest { MotionId = motionId, MemberId = officerId, Vote = VoteType.Approve });
        await Assert.That(vote.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    private static MembershipApplicationDTO ApplicationDto(Guid divisionId) => new() {
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
        EntryDate = DateOnly.FromDateTime(DateTime.UtcNow)
    };

    [Test]
    public async Task Forwarding_target_is_next_quartermaster_chapter() {
        var chain = Builder.SeedChapterHierarchy("Bund", "Land", "Kreis");
        var (bund, land, kreis) = (chain[0], chain[1], chain[2]);
        EnableApplicationForwarding();
        SetApplicationMode(land.Id, ForwardingMode.Redmine);
        SetApplicationMode(kreis.Id, ForwardingMode.Quartermaster);
        var application = Builder.SeedMembershipApplication(kreis.Id);
        var old = SeedMotion(kreis.Id, applicationId: application.Id);

        MarkAndHandOver(kreis.Id, noMemberAdministration: true, unableToAct: false);

        await Assert.That(Replacement(old).ChapterId).IsEqualTo(bund.Id);
        await Assert.That(Db.MembershipApplications.Single(a => a.Id == application.Id).ChapterId).IsEqualTo(bund.Id);
        await Assert.That(Db.ForwardingSteps.Count()).IsEqualTo(0);
    }

    /// <summary>The application dialog stores its decision before it closes the motion; a hand-over in between must not move the decided application.</summary>
    [Test]
    public async Task Application_decided_before_its_motion_was_closed_aborts_the_handover() {
        var chain = Builder.SeedChapterHierarchy("Bund", "Land", "Kreis");
        var kreis = chain[2];
        var application = Builder.SeedMembershipApplication(kreis.Id, status: ApplicationStatus.Rejected);
        var pending = SeedMotion(kreis.Id, applicationId: application.Id);

        await Assert.That(() => MarkAndHandOver(kreis.Id, noMemberAdministration: true, unableToAct: false)).Throws<InvalidOperationException>();

        var reloaded = Reload(pending.Id);
        await Assert.That(reloaded.ApprovalStatus).IsEqualTo(MotionApprovalStatus.Pending);
        await Assert.That(reloaded.HandedOverToMotionId).IsNull();
        await Assert.That(Db.Motions.Count()).IsEqualTo(1);
        await Assert.That(Db.MembershipApplications.Single(a => a.Id == application.Id).ChapterId).IsEqualTo(kreis.Id);
        await Assert.That(Db.Chapters.Single(c => c.Id == kreis.Id).NoMemberAdministration).IsFalse();
    }

    [Test]
    public async Task Decided_motion_is_skipped() {
        var chain = Builder.SeedChapterHierarchy("Bund", "Land", "Kreis");
        var kreis = chain[2];
        var application = Builder.SeedMembershipApplication(kreis.Id, status: ApplicationStatus.Approved);
        var approved = SeedMotion(kreis.Id, applicationId: application.Id);
        Db.Motions.Where(m => m.Id == approved.Id).Set(m => m.ApprovalStatus, MotionApprovalStatus.Approved).Update();

        var count = MarkAndHandOver(kreis.Id, noMemberAdministration: false, unableToAct: true);

        await Assert.That(count).IsEqualTo(0);
        var reloaded = Reload(approved.Id);
        await Assert.That(reloaded.ApprovalStatus).IsEqualTo(MotionApprovalStatus.Approved);
        await Assert.That(reloaded.HandedOverToMotionId).IsNull();
        await Assert.That(Db.Motions.Count()).IsEqualTo(1);
        await Assert.That(Db.MembershipApplications.Single(a => a.Id == application.Id).ChapterId).IsEqualTo(kreis.Id);
    }

    [Test]
    public async Task Preview_groups_by_target() {
        var bund = Builder.SeedChapter("Bund");
        var land = Builder.SeedChapter("Land", parentChapterId: bund.Id, noMemberAdministration: true);
        var kreis = Builder.SeedChapter("Kreis", parentChapterId: land.Id);
        SeedMotion(kreis.Id, applicationId: Builder.SeedMembershipApplication(kreis.Id).Id);
        SeedMotion(kreis.Id, dueSelectionId: Builder.SeedDueSelection().Id);
        SeedMotion(kreis.Id);
        SeedMotion(kreis.Id);

        using var scope = Factory.Services.CreateScope();
        var handover = scope.ServiceProvider.GetRequiredService<MotionHandover>();
        var unableToAct = handover.Preview(kreis.Id, noMemberAdministration: false, unableToAct: true);
        var noMemberAdministration = handover.Preview(kreis.Id, noMemberAdministration: true, unableToAct: false);
        var none = handover.Preview(kreis.Id, noMemberAdministration: false, unableToAct: false);

        await Assert.That(unableToAct.Count).IsEqualTo(2);
        await Assert.That(unableToAct.Single(p => p.TargetChapterId == bund.Id)).IsEqualTo(new HandoverPreview(bund.Id, "Bund", 2));
        await Assert.That(unableToAct.Single(p => p.TargetChapterId == land.Id)).IsEqualTo(new HandoverPreview(land.Id, "Land", 2));
        await Assert.That(noMemberAdministration.Count).IsEqualTo(1);
        await Assert.That(noMemberAdministration[0]).IsEqualTo(new HandoverPreview(bund.Id, "Bund", 2));
        await Assert.That(none.Count).IsEqualTo(0);
        var storedKreis = Db.Chapters.Single(c => c.Id == kreis.Id);
        await Assert.That(storedKreis.NoMemberAdministration).IsFalse();
        await Assert.That(storedKreis.UnableToAct).IsFalse();
        await Assert.That(Db.Motions.Count(m => m.ChapterId == kreis.Id && m.ApprovalStatus == MotionApprovalStatus.Pending)).IsEqualTo(4);
    }
}
