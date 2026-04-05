using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.ChapterAssociates;

public class ChapterOfficerDeleteEndpointTests : IntegrationTestBase {
    private static HttpRequestMessage BuildDeleteRequest(Guid memberId, Guid chapterId) {
        var req = new HttpRequestMessage(HttpMethod.Delete, "/api/chapterofficers");
        req.Content = new StringContent(
            JsonSerializer.Serialize(new { MemberId = memberId, ChapterId = chapterId }),
            Encoding.UTF8, "application/json");
        return req;
    }

    [Test]
    public async Task Returns_401_when_anonymous() {
        var chapter = Builder.SeedChapter("C");
        var member = Builder.SeedMember(chapter.Id);
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.SendAsync(BuildDeleteRequest(member.Id, chapter.Id));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_edit_officers() {
        var chapter = Builder.SeedChapter("C");
        var member = Builder.SeedMember(chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.SendAsync(BuildDeleteRequest(member.Id, chapter.Id));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Deletes_officer_row() {
        var chapter = Builder.SeedChapter("C");
        var member = Builder.SeedMember(chapter.Id);
        Builder.SeedChapterOfficer(member.Id, chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.EditOfficers });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.SendAsync(BuildDeleteRequest(member.Id, chapter.Id));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var stillExists = Db.ChapterOfficers.Any(o => o.MemberId == member.Id && o.ChapterId == chapter.Id);
        await Assert.That(stillExists).IsFalse();
    }

    [Test]
    public async Task Revokes_officer_role_from_linked_user() {
        var chapter = Builder.SeedChapter("C");
        var user = Builder.SeedUser();
        var member = Builder.SeedMember(chapter.Id, userId: user.Id);
        Builder.SeedChapterOfficer(member.Id, chapter.Id);

        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.EditOfficers });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        // After the factory starts, the "chapter_officer" system role has been seeded.
        var officerRole = Db.Roles.First(r => r.Identifier == PermissionIdentifier.SystemRole.ChapterOfficer);
        Builder.AssignRoleToUser(user.Id, officerRole.Id, chapter.Id);

        var response = await client.SendAsync(BuildDeleteRequest(member.Id, chapter.Id));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var assignmentStillExists = Db.UserRoleAssignments.Any(a =>
            a.UserId == user.Id && a.RoleId == officerRole.Id && a.ChapterId == chapter.Id);
        await Assert.That(assignmentStillExists).IsFalse();
    }

    [Test]
    public async Task Returns_OK_for_nonexistent_officer() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.EditOfficers });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.SendAsync(BuildDeleteRequest(Guid.NewGuid(), Guid.NewGuid()));
        // endpoint best-effort deletes, returning OK even if no row matched
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}
