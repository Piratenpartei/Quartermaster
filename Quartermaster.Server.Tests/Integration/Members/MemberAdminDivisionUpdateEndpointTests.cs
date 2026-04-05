using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Members;

public class MemberAdminDivisionUpdateEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        var chapter = Builder.SeedChapter();
        var member = Builder.SeedMember(chapter.Id);
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PutAsJsonAsync($"/api/members/{member.Id}/admindivision",
            new { ResidenceAdministrativeDivisionId = Guid.NewGuid() });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_view_all_members_permission() {
        var chapter = Builder.SeedChapter();
        var member = Builder.SeedMember(chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/members/{member.Id}/admindivision",
            new { ResidenceAdministrativeDivisionId = Guid.NewGuid() });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_404_when_member_not_found() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewAllMembers });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/members/{Guid.NewGuid()}/admindivision",
            new { ResidenceAdministrativeDivisionId = (Guid?)null });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Returns_400_when_division_not_found() {
        var chapter = Builder.SeedChapter();
        var member = Builder.SeedMember(chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewAllMembers });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/members/{member.Id}/admindivision",
            new { ResidenceAdministrativeDivisionId = Guid.NewGuid() });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Updates_residence_division_on_member() {
        var chapter = Builder.SeedChapter();
        var member = Builder.SeedMember(chapter.Id);
        var div = Builder.SeedAdminDivision("New Division");
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewAllMembers });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/members/{member.Id}/admindivision",
            new { ResidenceAdministrativeDivisionId = div.Id });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var updated = Db.Members.First(m => m.Id == member.Id);
        await Assert.That(updated.ResidenceAdministrativeDivisionId).IsEqualTo(div.Id);
    }

    [Test]
    public async Task Clears_residence_division_when_null_sent() {
        var chapter = Builder.SeedChapter();
        var div = Builder.SeedAdminDivision("Existing");
        var member = Builder.SeedMember(chapter.Id);
        member.ResidenceAdministrativeDivisionId = div.Id;
        Db.Update(member);
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewAllMembers });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/members/{member.Id}/admindivision",
            new { ResidenceAdministrativeDivisionId = (Guid?)null });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var updated = Db.Members.First(m => m.Id == member.Id);
        await Assert.That(updated.ResidenceAdministrativeDivisionId).IsNull();
    }
}
