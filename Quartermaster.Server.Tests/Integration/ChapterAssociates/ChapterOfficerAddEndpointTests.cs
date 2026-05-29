using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.ChapterAssociates;
using Quartermaster.Api.Motions;
using Quartermaster.Data.ChapterAssociates;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.ChapterAssociates;

public class ChapterOfficerAddEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        var chapter = Builder.SeedChapter("C");
        var member = Builder.SeedMember(chapter.Id);
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsJsonAsync("/api/chapterofficers", new ChapterOfficerAddRequest {
            MemberId = member.Id,
            ChapterId = chapter.Id,
            AssociateType = ChapterOfficerType.Captain
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_edit_officers() {
        var chapter = Builder.SeedChapter("C");
        var member = Builder.SeedMember(chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/chapterofficers", new ChapterOfficerAddRequest {
            MemberId = member.Id,
            ChapterId = chapter.Id,
            AssociateType = ChapterOfficerType.Captain
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_400_for_invalid_associate_type() {
        var chapter = Builder.SeedChapter("C");
        var member = Builder.SeedMember(chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.EditOfficers });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/chapterofficers", new ChapterOfficerAddRequest {
            MemberId = member.Id,
            ChapterId = chapter.Id,
            AssociateType = (ChapterOfficerType)99
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Returns_400_when_ids_empty() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.EditOfficers });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/chapterofficers", new ChapterOfficerAddRequest {
            MemberId = Guid.Empty,
            ChapterId = Guid.Empty,
            AssociateType = ChapterOfficerType.Captain
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Creates_officer_row() {
        var chapter = Builder.SeedChapter("C");
        var member = Builder.SeedMember(chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.EditOfficers });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/chapterofficers", new ChapterOfficerAddRequest {
            MemberId = member.Id,
            ChapterId = chapter.Id,
            AssociateType = ChapterOfficerType.Treasurer
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var officer = Db.ChapterOfficers.FirstOrDefault(o => o.MemberId == member.Id && o.ChapterId == chapter.Id);
        await Assert.That(officer).IsNotNull();
        await Assert.That(officer!.AssociateType).IsEqualTo(ChapterOfficerType.Treasurer);
    }

    [Test]
    public async Task Assigns_officer_role_to_linked_user() {
        var chapter = Builder.SeedChapter("C");
        var user = Builder.SeedUser();
        var member = Builder.SeedMember(chapter.Id, userId: user.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.EditOfficers });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/chapterofficers", new ChapterOfficerAddRequest {
            MemberId = member.Id,
            ChapterId = chapter.Id,
            AssociateType = ChapterOfficerType.Captain
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        // System role "chapter_officer" should have been assigned to the linked user
        var hasAssignment = Db.UserRoleAssignments.Any(a => a.UserId == user.Id && a.ChapterId == chapter.Id);
        await Assert.That(hasAssignment).IsTrue();
    }

    [Test]
    public async Task Adding_officer_does_not_create_a_user_but_officer_is_votable() {
        var chapter = Builder.SeedChapter("C");
        var member = Builder.SeedMember(chapter.Id, email: "officer@test.local");
        var (_, adminToken) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.EditOfficers, PermissionIdentifier.SystemVote });
        using var admin = await AuthenticatedClientWithCsrfAsync(adminToken);

        var addResponse = await admin.PostAsJsonAsync("/api/chapterofficers", new ChapterOfficerAddRequest {
            MemberId = member.Id,
            ChapterId = chapter.Id,
            AssociateType = ChapterOfficerType.Captain
        });
        await Assert.That(addResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // No user account is minted for the officer — that's SSO's job.
        var updatedMember = Db.Members.Single(m => m.Id == member.Id);
        await Assert.That(updatedMember.UserId).IsNull();

        // Yet the officer can immediately be voted for (vote belongs to the member).
        var motion = Builder.SeedMotion(chapter.Id);
        var voteResponse = await admin.PostAsJsonAsync("/api/motions/vote", new MotionVoteRequest {
            MotionId = motion.Id,
            MemberId = member.Id,
            Vote = VoteType.Approve
        });
        await Assert.That(voteResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Db.MotionVotes.Any(v => v.MotionId == motion.Id && v.MemberId == member.Id)).IsTrue();
    }

    [Test]
    public async Task Rejects_member_from_a_different_chapter_and_does_not_create_officer_or_grant_permissions() {
        var chapterA = Builder.SeedChapter("A");
        var chapterB = Builder.SeedChapter("B");
        var memberInB = Builder.SeedMember(chapterB.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapterA.Id] = new[] { PermissionIdentifier.EditOfficers } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PostAsJsonAsync("/api/chapterofficers", new ChapterOfficerAddRequest {
            MemberId = memberInB.Id,
            ChapterId = chapterA.Id,
            AssociateType = ChapterOfficerType.Captain
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var officer = Db.ChapterOfficers.FirstOrDefault(o => o.MemberId == memberInB.Id);
        await Assert.That(officer).IsNull();
    }

    [Test]
    public async Task Rejects_nonexistent_member() {
        var chapter = Builder.SeedChapter("A");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditOfficers } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PostAsJsonAsync("/api/chapterofficers", new ChapterOfficerAddRequest {
            MemberId = Guid.NewGuid(),
            ChapterId = chapter.Id,
            AssociateType = ChapterOfficerType.Captain
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Chapter_edit_officers_perm_suffices() {
        var chapter = Builder.SeedChapter("C");
        var member = Builder.SeedMember(chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditOfficers } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/chapterofficers", new ChapterOfficerAddRequest {
            MemberId = member.Id,
            ChapterId = chapter.Id,
            AssociateType = ChapterOfficerType.Captain
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}
