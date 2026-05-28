using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.Meetings;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Api.Motions;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Motions;

public class MotionDecisionMailTests : IntegrationTestBase {
    private void LinkMotionToApplication(Guid motionId, Guid applicationId) {
        Db.Motions.Where(m => m.Id == motionId)
            .Set(m => m.LinkedMembershipApplicationId, (Guid?)applicationId)
            .Update();
    }

    [Test]
    public async Task Approving_linked_motion_via_vote_resolves_application_and_sends_mail() {
        var chapter = Builder.SeedChapter("C");
        var app = Builder.SeedMembershipApplication(chapter.Id, email: "applicant@test.local");
        var motion = Builder.SeedMotion(chapter.Id);
        LinkMotionToApplication(motion.Id, app.Id);

        // One officer makes the majority denominator 1, so a single approve vote resolves it.
        var officer = Builder.SeedUser();
        var officerMember = Builder.SeedMember(chapter.Id, firstName: "O", lastName: "fficer", userId: officer.Id);
        Builder.SeedChapterOfficer(officerMember.Id, chapter.Id);

        var (voter, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.VoteMotions } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PostAsJsonAsync("/api/motions/vote", new MotionVoteRequest {
            MotionId = motion.Id,
            UserId = voter.Id,
            Vote = VoteType.Approve
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var updated = Db.MembershipApplications.First(a => a.Id == app.Id);
        await Assert.That(updated.Status).IsEqualTo(ApplicationStatus.Approved);

        var logs = Db.NotificationLogs.Where(l => l.TriggerId == "application_approved").ToList();
        await Assert.That(logs.Count).IsEqualTo(1);
        await Assert.That(logs[0].Recipient).IsEqualTo("applicant@test.local");
    }

    [Test]
    public async Task Completing_meeting_resolves_linked_application_and_sends_mail() {
        var chapter = Builder.SeedChapter("C");
        var app = Builder.SeedMembershipApplication(chapter.Id, email: "applicant@test.local");
        var motion = Builder.SeedMotion(chapter.Id);
        LinkMotionToApplication(motion.Id, app.Id);

        var meeting = Builder.SeedMeeting(chapter.Id, status: MeetingStatus.InProgress, meetingDate: DateTime.UtcNow);
        Builder.SeedAgendaItem(meeting.Id, itemType: AgendaItemType.Motion, motionId: motion.Id);

        // A single approve vote tips DetermineApprovalStatus to Approved on meeting completion.
        var voter = Builder.SeedUser();
        Builder.SeedMotionVote(motion.Id, voter.Id, VoteType.Approve);

        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PutAsJsonAsync($"/api/meetings/{meeting.Id}/status", new {
            Status = MeetingStatus.Completed
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var updated = Db.MembershipApplications.First(a => a.Id == app.Id);
        await Assert.That(updated.Status).IsEqualTo(ApplicationStatus.Approved);

        var logs = Db.NotificationLogs.Where(l => l.TriggerId == "application_approved").ToList();
        await Assert.That(logs.Count).IsEqualTo(1);
        await Assert.That(logs[0].Recipient).IsEqualTo("applicant@test.local");
    }
}
