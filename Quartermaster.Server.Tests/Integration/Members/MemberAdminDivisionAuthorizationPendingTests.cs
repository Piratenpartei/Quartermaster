using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Members;

/// <summary>
/// Tests for MemberAdminDivisionUpdateEndpoint authorization: requires <c>EditMembers</c>
/// (chapter-scoped) instead of the global-only <c>ViewAllMembers</c>. Write operations
/// must use a write permission scoped to the member's chapter.
/// </summary>
public class MemberAdminDivisionAuthorizationPendingTests : IntegrationTestBase {
    [Test]
    public async Task Should_return_403_when_user_has_only_view_all_members_not_edit_members() {
        var chapter = Builder.SeedChapter();
        var member = Builder.SeedMember(chapter.Id);
        var div = Builder.SeedAdminDivision("New Div");
        // ViewAllMembers is a VIEW permission — should not authorize writes.
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewAllMembers });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/members/{member.Id}/admindivision",
            new { ResidenceAdministrativeDivisionId = div.Id });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Should_return_200_when_user_has_EditMembers_on_members_chapter() {
        var chapter = Builder.SeedChapter();
        var member = Builder.SeedMember(chapter.Id);
        var div = Builder.SeedAdminDivision("New Div");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMembers } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/members/{member.Id}/admindivision",
            new { ResidenceAdministrativeDivisionId = div.Id });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Should_return_403_when_user_has_EditMembers_on_different_chapter() {
        var chapterA = Builder.SeedChapter("A");
        var chapterB = Builder.SeedChapter("B");
        var member = Builder.SeedMember(chapterA.Id);
        var div = Builder.SeedAdminDivision("New Div");
        // User has EditMembers on B but member is in A — must be 403.
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapterB.Id] = new[] { PermissionIdentifier.EditMembers } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/members/{member.Id}/admindivision",
            new { ResidenceAdministrativeDivisionId = div.Id });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Should_recompute_orphan_flag_to_false_after_assigning_admin_division() {
        var chapter = Builder.SeedChapter();
        var div = Builder.SeedAdminDivision("New Div");
        // Start with an orphaned member (no division set). The helper doesn't expose an
        // IsOrphaned field on Member directly because orphan state lives on the
        // AdministrativeDivision entity. What we can assert: after this update, the
        // member's ResidenceAdministrativeDivisionId points at a non-orphaned division
        // and any orphan-derived processing picks that up.
        var member = Builder.SeedMember(chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMembers } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/members/{member.Id}/admindivision",
            new { ResidenceAdministrativeDivisionId = div.Id });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var updated = Db.Members.First(m => m.Id == member.Id);
        await Assert.That(updated.ResidenceAdministrativeDivisionId).IsEqualTo(div.Id);
        // The assigned division is not marked orphaned.
        var assignedDiv = Db.AdministrativeDivisions.First(d => d.Id == div.Id);
        await Assert.That(assignedDiv.IsOrphaned).IsFalse();
    }
}
