using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.ChapterAssociates;
using Quartermaster.Api.Motions;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Motions;

public class MotionVoteEndpointTests : IntegrationTestBase {
    private Guid SeedOfficerMember(Guid chapterId, Guid? userId = null) {
        var member = Builder.SeedMember(chapterId, userId: userId);
        Builder.SeedChapterOfficer(member.Id, chapterId);
        return member.Id;
    }

    [Test]
    public async Task Returns_401_when_anonymous() {
        var chapter = Builder.SeedChapter();
        var motion = Builder.SeedMotion(chapter.Id);
        var officerMemberId = SeedOfficerMember(chapter.Id);
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsJsonAsync("/api/motions/vote", new MotionVoteRequest {
            MotionId = motion.Id,
            MemberId = officerMemberId,
            Vote = VoteType.Approve
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_vote_motions_permission() {
        var chapter = Builder.SeedChapter();
        var motion = Builder.SeedMotion(chapter.Id);
        var officerMemberId = SeedOfficerMember(chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/motions/vote", new MotionVoteRequest {
            MotionId = motion.Id,
            MemberId = officerMemberId,
            Vote = VoteType.Approve
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_404_for_nonexistent_motion() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/motions/vote", new MotionVoteRequest {
            MotionId = Guid.NewGuid(),
            MemberId = Guid.NewGuid(),
            Vote = VoteType.Approve
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Returns_400_when_vote_value_out_of_range() {
        var chapter = Builder.SeedChapter();
        var motion = Builder.SeedMotion(chapter.Id);
        var officerMemberId = SeedOfficerMember(chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/motions/vote", new MotionVoteRequest {
            MotionId = motion.Id,
            MemberId = officerMemberId,
            Vote = (VoteType)99
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Returns_400_when_target_is_not_an_officer() {
        var chapter = Builder.SeedChapter();
        var motion = Builder.SeedMotion(chapter.Id);
        var nonOfficer = Builder.SeedMember(chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.SystemVote });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/motions/vote", new MotionVoteRequest {
            MotionId = motion.Id,
            MemberId = nonOfficer.Id,
            Vote = VoteType.Approve
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Self_vote_is_allowed_with_permission() {
        var chapter = Builder.SeedChapter();
        var motion = Builder.SeedMotion(chapter.Id);
        var (user, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.VoteMotions } });
        var ownMemberId = SeedOfficerMember(chapter.Id, userId: user.Id);
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PostAsJsonAsync("/api/motions/vote", new MotionVoteRequest {
            MotionId = motion.Id,
            MemberId = ownMemberId,
            Vote = VoteType.Approve
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var persisted = Db.MotionVotes.FirstOrDefault(v => v.MotionId == motion.Id && v.MemberId == ownMemberId);
        await Assert.That(persisted).IsNotNull();
        await Assert.That(persisted!.Vote).IsEqualTo(VoteType.Approve);
        await Assert.That(persisted.CastByUserId).IsEqualTo(user.Id);
    }

    [Test]
    public async Task System_vote_holder_can_record_vote_for_any_officer() {
        var chapter = Builder.SeedChapter();
        var motion = Builder.SeedMotion(chapter.Id);
        var officerMemberId = SeedOfficerMember(chapter.Id);
        var (admin, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.SystemVote });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PostAsJsonAsync("/api/motions/vote", new MotionVoteRequest {
            MotionId = motion.Id,
            MemberId = officerMemberId,
            Vote = VoteType.Approve
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var persisted = Db.MotionVotes.FirstOrDefault(v => v.MotionId == motion.Id && v.MemberId == officerMemberId);
        await Assert.That(persisted).IsNotNull();
        await Assert.That(persisted!.CastByUserId).IsEqualTo(admin.Id);
    }

    [Test]
    public async Task Delegation_fails_when_caller_not_officer_and_lacks_delegate() {
        var chapter = Builder.SeedChapter();
        var motion = Builder.SeedMotion(chapter.Id);
        var officerMemberId = SeedOfficerMember(chapter.Id);
        // Caller has VoteMotions but is neither an officer nor a delegate.
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.VoteMotions } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/motions/vote", new MotionVoteRequest {
            MotionId = motion.Id,
            MemberId = officerMemberId,
            Vote = VoteType.Approve
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Delegation_succeeds_with_delegate_permission() {
        var chapter = Builder.SeedChapter();
        var motion = Builder.SeedMotion(chapter.Id);
        var officerMemberId = SeedOfficerMember(chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] {
                PermissionIdentifier.VoteMotions, PermissionIdentifier.VoteDelegateMotions
            } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/motions/vote", new MotionVoteRequest {
            MotionId = motion.Id,
            MemberId = officerMemberId,
            Vote = VoteType.Approve
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var persisted = Db.MotionVotes.FirstOrDefault(v => v.MotionId == motion.Id && v.MemberId == officerMemberId);
        await Assert.That(persisted).IsNotNull();
    }
}
