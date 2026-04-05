using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Motions;
using Quartermaster.Data.Motions;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Motions;

public class MotionVoteEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        var chapter = Builder.SeedChapter();
        var motion = Builder.SeedMotion(chapter.Id);
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsJsonAsync("/api/motions/vote", new MotionVoteRequest {
            MotionId = motion.Id,
            UserId = Guid.NewGuid(),
            Vote = (int)VoteType.Approve
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_vote_motions_permission() {
        var chapter = Builder.SeedChapter();
        var motion = Builder.SeedMotion(chapter.Id);
        var (user, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/motions/vote", new MotionVoteRequest {
            MotionId = motion.Id,
            UserId = user.Id,
            Vote = (int)VoteType.Approve
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_404_for_nonexistent_motion() {
        var (user, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/motions/vote", new MotionVoteRequest {
            MotionId = Guid.NewGuid(),
            UserId = user.Id,
            Vote = (int)VoteType.Approve
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Returns_400_when_vote_value_out_of_range() {
        var chapter = Builder.SeedChapter();
        var motion = Builder.SeedMotion(chapter.Id);
        var (user, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/motions/vote", new MotionVoteRequest {
            MotionId = motion.Id,
            UserId = user.Id,
            Vote = 99
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Self_vote_is_allowed_with_permission() {
        var chapter = Builder.SeedChapter();
        var motion = Builder.SeedMotion(chapter.Id);
        var (user, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.VoteMotions } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/motions/vote", new MotionVoteRequest {
            MotionId = motion.Id,
            UserId = user.Id,
            Vote = (int)VoteType.Approve
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var persisted = Db.MotionVotes.FirstOrDefault(v => v.MotionId == motion.Id && v.UserId == user.Id);
        await Assert.That(persisted).IsNotNull();
        await Assert.That(persisted!.Vote).IsEqualTo(VoteType.Approve);
    }

    [Test]
    public async Task Delegation_fails_when_target_is_not_officer() {
        var chapter = Builder.SeedChapter();
        var motion = Builder.SeedMotion(chapter.Id);
        var (caller, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.VoteMotions } });
        var target = Builder.SeedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/motions/vote", new MotionVoteRequest {
            MotionId = motion.Id,
            UserId = target.Id,
            Vote = (int)VoteType.Approve
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Delegation_fails_when_caller_lacks_delegate_permission() {
        var chapter = Builder.SeedChapter();
        var motion = Builder.SeedMotion(chapter.Id);
        // Caller has VoteMotions but is not an officer and lacks delegate perm.
        var (caller, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.VoteMotions } });
        // Target is an officer.
        var target = Builder.SeedUser();
        var targetMember = Builder.SeedMember(chapter.Id, firstName: "T", lastName: "Target", userId: target.Id);
        Builder.SeedChapterOfficer(targetMember.Id, chapter.Id);
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/motions/vote", new MotionVoteRequest {
            MotionId = motion.Id,
            UserId = target.Id,
            Vote = (int)VoteType.Approve
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Delegation_succeeds_with_delegate_permission_and_officer_target() {
        var chapter = Builder.SeedChapter();
        var motion = Builder.SeedMotion(chapter.Id);
        // Caller has both VoteMotions and VoteDelegateMotions globally.
        var (caller, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] {
                PermissionIdentifier.VoteMotions, PermissionIdentifier.VoteDelegateMotions
            } });
        // Target is an officer.
        var target = Builder.SeedUser();
        var targetMember = Builder.SeedMember(chapter.Id, firstName: "T", lastName: "Target", userId: target.Id);
        Builder.SeedChapterOfficer(targetMember.Id, chapter.Id);
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/motions/vote", new MotionVoteRequest {
            MotionId = motion.Id,
            UserId = target.Id,
            Vote = (int)VoteType.Approve
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var persisted = Db.MotionVotes.FirstOrDefault(v => v.MotionId == motion.Id && v.UserId == target.Id);
        await Assert.That(persisted).IsNotNull();
    }
}
