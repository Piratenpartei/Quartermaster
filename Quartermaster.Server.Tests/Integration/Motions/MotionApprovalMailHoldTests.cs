using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.Forwarding;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Api.Motions;
using Quartermaster.Data.Forwarding;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Motions;

public class MotionApprovalMailHoldTests : IntegrationTestBase {
    private async Task<Guid> VoteOnLinkedApplicationAsync(VoteType vote) {
        var chapter = Builder.SeedChapter("C");
        var application = Builder.SeedMembershipApplication(chapter.Id, email: "applicant@test.local");
        var motion = Builder.SeedMotion(chapter.Id);
        Db.Motions.Where(m => m.Id == motion.Id).Set(m => m.LinkedMembershipApplicationId, (Guid?)application.Id).Update();
        new ForwardingCaseRepository(Db).Create(
            new ForwardingCase { Kind = ForwardingCaseKind.Application, MembershipApplicationId = application.Id, MotionId = motion.Id, CreatedAt = DateTime.UtcNow },
            [
                new ForwardingStep { Order = 1, Kind = ForwardingStepKind.CreateTicket, TargetChapterId = chapter.Id, Purpose = ForwardingPurpose.Application, State = ForwardingStepState.Done, UpdatedAt = DateTime.UtcNow },
                new ForwardingStep { Order = 2, Kind = ForwardingStepKind.AddOutcomeNote, TargetChapterId = chapter.Id, Purpose = ForwardingPurpose.Application, State = ForwardingStepState.Waiting, UpdatedAt = DateTime.UtcNow },
                new ForwardingStep { Order = 3, Kind = ForwardingStepKind.AwaitSageEntry, TargetChapterId = chapter.Id, Purpose = ForwardingPurpose.Application, State = ForwardingStepState.Waiting, UpdatedAt = DateTime.UtcNow }
            ]);
        var officer = Builder.SeedMember(chapter.Id, firstName: "O", lastName: "fficer");
        Builder.SeedChapterOfficer(officer.Id, chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: new[] { PermissionIdentifier.SystemVote });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PostAsJsonAsync("/api/motions/vote", new MotionVoteRequest { MotionId = motion.Id, MemberId = officer.Id, Vote = vote });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        return application.Id;
    }

    [Test]
    public async Task Approval_by_vote_with_sage_step_sends_no_mail() {
        var applicationId = await VoteOnLinkedApplicationAsync(VoteType.Approve);

        await Assert.That(Db.MembershipApplications.Single(a => a.Id == applicationId).Status).IsEqualTo(ApplicationStatus.Approved);
        await Assert.That(Db.NotificationLogs.Count(l => l.TriggerId == "application_approved")).IsEqualTo(0);
    }

    [Test]
    public async Task Rejection_by_vote_with_sage_step_mails_at_once() {
        var applicationId = await VoteOnLinkedApplicationAsync(VoteType.Deny);

        await Assert.That(Db.MembershipApplications.Single(a => a.Id == applicationId).Status).IsEqualTo(ApplicationStatus.Rejected);
        var logs = Db.NotificationLogs.Where(l => l.TriggerId == "application_rejected").ToList();
        await Assert.That(logs.Count).IsEqualTo(1);
        await Assert.That(logs[0].Recipient).IsEqualTo("applicant@test.local");
    }
}
