using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Microsoft.Extensions.DependencyInjection;
using Quartermaster.Api;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.Forwarding;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Api.Motions;
using Quartermaster.Data;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Forwarding;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Motions;
using Quartermaster.Server.Chapters;
using Quartermaster.Server.Tests.Forwarding.Fakes;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Chapters;

[NotInParallel("FakeRedmineClient")]
public class TakeOverSweepTests : IntegrationTestBase {
    private const string SubmittedTriggerId = "application_submitted";
    private static readonly DateTime SubmittedAt = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly Chapter _root;
    private readonly Chapter _state;
    private readonly Chapter _county;

    public TakeOverSweepTests() {
        DbContext.SupplementDefaults(Factory.Services);
        FakeRedmineClient.Reset();
        var chain = Builder.SeedChapterHierarchy("Bund", "Land", "Kreis");
        (_root, _state, _county) = (chain[0], chain[1], chain[2]);
        Db.Chapters.Where(c => c.Id == _state.Id).Set(c => c.Level, ChapterLevel.State).Update();
    }

    private int Sweep(int daysAfterSubmission) {
        using var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TakeOverSweep>().Run(SubmittedAt.AddDays(daysAfterSubmission));
    }

    private (MembershipApplication Application, Motion Motion) SeedPending(Guid? chapterId = null) {
        var chapter = chapterId ?? _county.Id;
        var application = Builder.SeedMembershipApplication(chapter);
        Db.MembershipApplications.Where(a => a.Id == application.Id).Set(a => a.SubmittedAt, SubmittedAt).Update();
        var motion = Motion.Create(chapter, "Anna Pirat", "anna@test.local", "Antrag", "Text", "<p>Text</p>", SubmittedAt, application.Id, null);
        motion.IsPublic = true;
        Db.Insert(motion);
        return (application, motion);
    }

    private Motion Reload(Guid motionId) {
        return Db.Motions.Single(m => m.Id == motionId);
    }

    private Motion Replacement(Motion old) {
        return Reload(Reload(old.Id).HandedOverToMotionId!.Value);
    }

    private void SetApplicationMode(Guid chapterId, ForwardingMode mode) {
        using var scope = Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ForwardingSettingsRepository>()
            .SaveChapter(new ForwardingChapterSetting { ChapterId = chapterId, ApplicationMode = mode }, []);
    }

    private async Task<MembershipApplication> SubmitApplicationAsync() {
        var (_, submitterToken) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(submitterToken);
        var submit = await client.PostAsJsonAsync("/api/membershipapplications", new MembershipApplicationDTO {
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
            AddressAdministrativeDivisionId = Builder.SeedDivisionFor(_county.Id),
            ConformityDeclarationAccepted = true,
            EntryDate = DateOnly.FromDateTime(DateTime.UtcNow)
        });
        await Assert.That(submit.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var application = Db.MembershipApplications.Single();
        Db.MembershipApplications.Where(a => a.Id == application.Id).Set(a => a.SubmittedAt, SubmittedAt).Update();
        return application;
    }

    [Test]
    public async Task Sweep_moves_due_motion_to_state() {
        SageSetupSeeder.Seed(Factory.Services, _root.Id, _state.Id);
        SetApplicationMode(_county.Id, ForwardingMode.Quartermaster);
        SetApplicationMode(_state.Id, ForwardingMode.Quartermaster);
        var application = await SubmitApplicationAsync();
        var old = Db.Motions.Single(m => m.LinkedMembershipApplicationId == application.Id);
        var forwardingCase = Db.ForwardingCases.Single(c => c.Kind == ForwardingCaseKind.Application && c.MembershipApplicationId == application.Id);

        var moved = Sweep(29);

        await Assert.That(moved).IsEqualTo(1);
        var closed = Reload(old.Id);
        await Assert.That(closed.ApprovalStatus).IsEqualTo(MotionApprovalStatus.ClosedWithoutAction);
        await Assert.That(closed.HandoverReason).IsEqualTo(MotionHandoverReason.Deadline);
        var replacement = Replacement(old);
        await Assert.That(replacement.ChapterId).IsEqualTo(_state.Id);
        await Assert.That(replacement.ApprovalStatus).IsEqualTo(MotionApprovalStatus.Pending);
        await Assert.That(replacement.LinkedMembershipApplicationId).IsEqualTo(application.Id);
        await Assert.That(replacement.Title).IsEqualTo(old.Title);
        await Assert.That(replacement.TextMarkdown).IsEqualTo(old.TextMarkdown);
        await Assert.That(replacement.IsPublic).IsEqualTo(old.IsPublic);
        await Assert.That(Db.MembershipApplications.Single(a => a.Id == application.Id).ChapterId).IsEqualTo(_state.Id);
        var pointed = Db.ForwardingCases.Single(c => c.Id == forwardingCase.Id);
        await Assert.That(pointed.MotionId).IsEqualTo(replacement.Id);
        await Assert.That(pointed.DecidingChapterId).IsEqualTo(_state.Id);
    }

    [Test]
    public async Task Sweep_leaves_motion_before_deadline() {
        var (application, motion) = SeedPending();

        var moved = Sweep(27);

        await Assert.That(moved).IsEqualTo(0);
        await Assert.That(Reload(motion.Id).ApprovalStatus).IsEqualTo(MotionApprovalStatus.Pending);
        await Assert.That(Db.Motions.Count()).IsEqualTo(1);
        await Assert.That(Db.MembershipApplications.Single(a => a.Id == application.Id).ChapterId).IsEqualTo(_county.Id);
    }

    [Test]
    public async Task Sweep_moves_straight_to_highest_due() {
        var (_, motion) = SeedPending();

        var moved = Sweep(63);

        await Assert.That(moved).IsEqualTo(1);
        await Assert.That(Replacement(motion).ChapterId).IsEqualTo(_root.Id);
        await Assert.That(Db.Motions.Count()).IsEqualTo(2);
    }

    [Test]
    public async Task Sweep_moves_motion_with_votes() {
        var (_, motion) = SeedPending();
        var officer = Builder.SeedMember(_county.Id);
        var (voter, _) = Builder.SeedAuthenticatedUser();
        Builder.SeedMotionVote(motion.Id, officer.Id, voter.Id, VoteType.Approve);

        var moved = Sweep(29);

        await Assert.That(moved).IsEqualTo(1);
        var replacement = Replacement(motion);
        await Assert.That(Db.MotionVotes.Count(v => v.MotionId == replacement.Id)).IsEqualTo(0);
        await Assert.That(Db.MotionVotes.Count(v => v.MotionId == motion.Id)).IsEqualTo(1);
    }

    [Test]
    public async Task Vote_on_moved_old_motion_is_refused() {
        var (_, motion) = SeedPending();
        var officer = Builder.SeedMember(_county.Id);
        var (_, voterToken) = Builder.SeedAuthenticatedUser(globalPermissions: new[] { PermissionIdentifier.SystemVote });
        Sweep(29);
        var replacement = Replacement(motion);

        using var client = await AuthenticatedClientWithCsrfAsync(voterToken);
        var vote = await client.PostAsJsonAsync("/api/motions/vote",
            new MotionVoteRequest { MotionId = motion.Id, MemberId = officer.Id, Vote = VoteType.Approve });

        await Assert.That(vote.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(Db.MotionVotes.Count(v => v.MotionId == replacement.Id)).IsEqualTo(0);
        await Assert.That(Reload(replacement.Id).ApprovalStatus).IsEqualTo(MotionApprovalStatus.Pending);
    }

    [Test]
    public async Task Sweep_skips_decided_motion() {
        var (_, motion) = SeedPending();
        Db.Motions.Where(m => m.Id == motion.Id).Set(m => m.ApprovalStatus, MotionApprovalStatus.Approved).Update();

        var moved = Sweep(29);

        await Assert.That(moved).IsEqualTo(0);
        await Assert.That(Reload(motion.Id).HandedOverToMotionId).IsNull();
        await Assert.That(Db.Motions.Count()).IsEqualTo(1);
    }

    [Test]
    public async Task Sweep_leaves_reduction_motion() {
        var dueSelection = Builder.SeedDueSelection();
        var motion = Motion.Create(_county.Id, "Anna Pirat", "anna@test.local", "Reduzierung", "Text", "<p>Text</p>", SubmittedAt, null, dueSelection.Id);
        Db.Insert(motion);

        var moved = Sweep(63);

        await Assert.That(moved).IsEqualTo(0);
        await Assert.That(Reload(motion.Id).ApprovalStatus).IsEqualTo(MotionApprovalStatus.Pending);
        await Assert.That(Db.Motions.Count()).IsEqualTo(1);
    }

    [Test]
    public async Task Sweep_leaves_federal_approval_motion() {
        var application = Builder.SeedMembershipApplication(_county.Id, status: ApplicationStatus.PendingFederalApproval);
        Db.MembershipApplications.Where(a => a.Id == application.Id).Set(a => a.SubmittedAt, SubmittedAt).Update();
        var motion = Motion.Create(_root.Id, "Anna Pirat", "anna@test.local", "Bundesfreigabe", "Text", "<p>Text</p>", SubmittedAt, application.Id, null);
        Db.Insert(motion);
        Db.MembershipApplications.Where(a => a.Id == application.Id).Set(a => a.FederalApprovalMotionId, (Guid?)motion.Id).Update();

        var moved = Sweep(365);

        await Assert.That(moved).IsEqualTo(0);
        await Assert.That(Reload(motion.Id).ApprovalStatus).IsEqualTo(MotionApprovalStatus.Pending);
        await Assert.That(Db.Motions.Count()).IsEqualTo(1);
    }

    [Test]
    public async Task Deadline_move_keeps_on_behalf_of() {
        var (_, motion) = SeedPending();
        var origin = Builder.SeedChapter("Ort", parentChapterId: _county.Id);
        Db.Motions.Where(m => m.Id == motion.Id)
            .Set(m => m.OnBehalfOfChapterId, (Guid?)origin.Id)
            .Set(m => m.OnBehalfOfReason, (ChapterMark?)ChapterMark.UnableToAct)
            .Update();

        Sweep(29);

        var replacement = Replacement(motion);
        await Assert.That(replacement.OnBehalfOfChapterId).IsEqualTo(origin.Id);
        await Assert.That(replacement.OnBehalfOfReason).IsEqualTo(ChapterMark.UnableToAct);
    }

    [Test]
    public async Task Moved_motion_decides_application() {
        SageSetupSeeder.Seed(Factory.Services, _root.Id, _state.Id);
        SetApplicationMode(_county.Id, ForwardingMode.Quartermaster);
        SetApplicationMode(_state.Id, ForwardingMode.Quartermaster);
        var stateOfficer = Builder.SeedMember(_state.Id, firstName: "Landes", lastName: "Vorstand");
        Builder.SeedChapterOfficer(stateOfficer.Id, _state.Id);
        var (_, voterToken) = Builder.SeedAuthenticatedUser(globalPermissions: new[] { PermissionIdentifier.SystemVote });
        var application = await SubmitApplicationAsync();
        var old = Db.Motions.Single(m => m.LinkedMembershipApplicationId == application.Id);
        Sweep(29);
        var replacement = Replacement(old);

        using (var client = await AuthenticatedClientWithCsrfAsync(voterToken)) {
            var vote = await client.PostAsJsonAsync("/api/motions/vote",
                new MotionVoteRequest { MotionId = replacement.Id, MemberId = stateOfficer.Id, Vote = VoteType.Approve });
            await Assert.That(vote.StatusCode).IsEqualTo(HttpStatusCode.OK);
        }

        await Assert.That(Reload(replacement.Id).ApprovalStatus).IsEqualTo(MotionApprovalStatus.Approved);
        await Assert.That(Db.MembershipApplications.Single(a => a.Id == application.Id).Status).IsEqualTo(ApplicationStatus.Approved);
        await Assert.That(Reload(old.Id).ApprovalStatus).IsEqualTo(MotionApprovalStatus.ClosedWithoutAction);
    }

    [Test]
    public async Task Sweep_notifies_target_only() {
        Builder.SeedAuthenticatedUser(email: "land@test.local", chapterPermissions: new() { [_state.Id] = new[] { PermissionIdentifier.ProcessApplications } });
        Builder.SeedAuthenticatedUser(email: "kreis@test.local", chapterPermissions: new() { [_county.Id] = new[] { PermissionIdentifier.ProcessApplications } });
        SeedPending();

        Sweep(29);

        await Assert.That(Db.NotificationLogs.Count(l => l.TriggerId == SubmittedTriggerId && l.Recipient == "land@test.local")).IsEqualTo(1);
        await Assert.That(Db.NotificationLogs.Count(l => l.TriggerId == SubmittedTriggerId && l.Recipient == "kreis@test.local")).IsEqualTo(0);
    }
}
