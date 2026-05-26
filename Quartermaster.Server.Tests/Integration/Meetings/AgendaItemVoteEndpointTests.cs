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

public class AgendaItemVoteEndpointTests : IntegrationTestBase {
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
            $"/api/meetings/{meetingId}/agenda/{itemId}/vote",
            new AgendaItemVoteRequest { MeetingId = meetingId, ItemId = itemId, UserId = Guid.NewGuid(), Vote = VoteType.Approve });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_vote_permission() {
        var (_, meetingId, itemId, _) = SeedMotionAgendaItem();
        var (user, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meetingId}/agenda/{itemId}/vote",
            new AgendaItemVoteRequest { MeetingId = meetingId, ItemId = itemId, UserId = user.Id, Vote = VoteType.Approve });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_404_when_meeting_missing() {
        var (chapterId, _, _, _) = SeedMotionAgendaItem();
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapterId] = new[] { PermissionIdentifier.VoteMotions } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var fakeId = Guid.NewGuid();
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{fakeId}/agenda/{Guid.NewGuid()}/vote",
            new AgendaItemVoteRequest { MeetingId = fakeId, ItemId = Guid.NewGuid(), UserId = Guid.NewGuid(), Vote = VoteType.Approve });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Returns_400_when_meeting_not_in_progress() {
        var (chapterId, meetingId, itemId, _) = SeedMotionAgendaItem(MeetingStatus.Scheduled);
        var (user, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapterId] = new[] { PermissionIdentifier.VoteMotions } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meetingId}/agenda/{itemId}/vote",
            new AgendaItemVoteRequest { MeetingId = meetingId, ItemId = itemId, UserId = user.Id, Vote = VoteType.Approve });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Returns_404_when_item_not_in_meeting() {
        var (chapterId, meetingId, _, _) = SeedMotionAgendaItem();
        var (user, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapterId] = new[] { PermissionIdentifier.VoteMotions } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var fakeItem = Guid.NewGuid();
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meetingId}/agenda/{fakeItem}/vote",
            new AgendaItemVoteRequest { MeetingId = meetingId, ItemId = fakeItem, UserId = user.Id, Vote = VoteType.Approve });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Returns_400_when_item_is_not_motion_type() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, status: MeetingStatus.InProgress);
        var item = Builder.SeedAgendaItem(meeting.Id, itemType: AgendaItemType.Discussion);
        var (user, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.VoteMotions } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meeting.Id}/agenda/{item.Id}/vote",
            new AgendaItemVoteRequest { MeetingId = meeting.Id, ItemId = item.Id, UserId = user.Id, Vote = VoteType.Approve });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Casts_own_vote_and_persists_meeting_attribution() {
        var (chapterId, meetingId, itemId, motionId) = SeedMotionAgendaItem();
        var (user, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapterId] = new[] { PermissionIdentifier.VoteMotions } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meetingId}/agenda/{itemId}/vote",
            new AgendaItemVoteRequest { MeetingId = meetingId, ItemId = itemId, UserId = user.Id, Vote = VoteType.Approve });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var vote = Db.MotionVotes.FirstOrDefault(v => v.MotionId == motionId && v.UserId == user.Id);
        await Assert.That(vote).IsNotNull();
        await Assert.That(vote!.Vote).IsEqualTo(VoteType.Approve);
        await Assert.That(vote.MeetingId).IsEqualTo(meetingId);
    }

    [Test]
    public async Task Returns_400_when_proxying_for_non_officer() {
        var (chapterId, meetingId, itemId, _) = SeedMotionAgendaItem();
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapterId] = new[] {
                PermissionIdentifier.VoteMotions, PermissionIdentifier.VoteDelegateMotions } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var (otherUser, _) = Builder.SeedAuthenticatedUser();
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meetingId}/agenda/{itemId}/vote",
            new AgendaItemVoteRequest { MeetingId = meetingId, ItemId = itemId, UserId = otherUser.Id, Vote = VoteType.Approve });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task SystemVote_holder_can_vote_for_anyone() {
        var (_, meetingId, itemId, motionId) = SeedMotionAgendaItem();
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.SystemVote });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var (targetUser, _) = Builder.SeedAuthenticatedUser(firstName: "Target", lastName: "User");
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meetingId}/agenda/{itemId}/vote",
            new AgendaItemVoteRequest { MeetingId = meetingId, ItemId = itemId, UserId = targetUser.Id, Vote = VoteType.Deny });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var vote = Db.MotionVotes.FirstOrDefault(v => v.MotionId == motionId && v.UserId == targetUser.Id);
        await Assert.That(vote).IsNotNull();
        await Assert.That(vote!.Vote).IsEqualTo(VoteType.Deny);
    }
}
