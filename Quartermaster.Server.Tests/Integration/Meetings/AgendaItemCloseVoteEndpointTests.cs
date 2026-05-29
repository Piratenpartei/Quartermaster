using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Meetings;
using Quartermaster.Api.Motions;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Meetings;

public class AgendaItemCloseVoteEndpointTests : IntegrationTestBase {
    private (Guid chapterId, Guid meetingId, Guid itemId, Guid motionId) SeedMotionAgendaItem(
        MeetingStatus status = MeetingStatus.InProgress) {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, status: status);
        var motion = Builder.SeedMotion(chapter.Id);
        var item = Builder.SeedAgendaItem(meeting.Id, itemType: AgendaItemType.Motion, motionId: motion.Id);
        return (chapter.Id, meeting.Id, item.Id, motion.Id);
    }

    [Test]
    public async Task Returns_401_when_anonymous() {
        var (_, meetingId, itemId, _) = SeedMotionAgendaItem();
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meetingId}/agenda/{itemId}/close-vote", new { meetingId, itemId });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_EditMeetings() {
        var (_, meetingId, itemId, _) = SeedMotionAgendaItem();
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meetingId}/agenda/{itemId}/close-vote", new { meetingId, itemId });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_404_when_meeting_missing() {
        var chapter = Builder.SeedChapter("C");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var fake = Guid.NewGuid();
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{fake}/agenda/{Guid.NewGuid()}/close-vote", new { });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Returns_400_when_meeting_not_in_progress() {
        var (chapterId, meetingId, itemId, _) = SeedMotionAgendaItem(MeetingStatus.Scheduled);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapterId] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meetingId}/agenda/{itemId}/close-vote", new { });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Returns_400_when_item_not_motion_type() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, status: MeetingStatus.InProgress);
        var item = Builder.SeedAgendaItem(meeting.Id, itemType: AgendaItemType.Discussion);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meeting.Id}/agenda/{item.Id}/close-vote", new { });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Closes_vote_and_records_resolution() {
        var (chapterId, meetingId, itemId, motionId) = SeedMotionAgendaItem();
        var voterMember = Builder.SeedMember(chapterId);
        var caster = Builder.SeedUser();
        Builder.SeedMotionVote(motionId, voterMember.Id, caster.Id, VoteType.Approve);

        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapterId] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meetingId}/agenda/{itemId}/close-vote", new { });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var updatedItem = Db.AgendaItems.First(a => a.Id == itemId);
        await Assert.That(updatedItem.Resolution).IsNotNull();
        await Assert.That(updatedItem.Resolution!.Contains("1 Ja")).IsTrue();
        var updatedMotion = Db.Motions.First(m => m.Id == motionId);
        await Assert.That(updatedMotion.ApprovalStatus).IsEqualTo(MotionApprovalStatus.Approved);
    }

    [Test]
    public async Task Returns_404_when_item_in_other_meeting() {
        var chapter = Builder.SeedChapter("C");
        var meetingA = Builder.SeedMeeting(chapter.Id, title: "A", status: MeetingStatus.InProgress);
        var meetingB = Builder.SeedMeeting(chapter.Id, title: "B", status: MeetingStatus.InProgress);
        var motion = Builder.SeedMotion(chapter.Id);
        var itemInB = Builder.SeedAgendaItem(meetingB.Id, itemType: AgendaItemType.Motion, motionId: motion.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meetingA.Id}/agenda/{itemInB.Id}/close-vote", new { });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}
