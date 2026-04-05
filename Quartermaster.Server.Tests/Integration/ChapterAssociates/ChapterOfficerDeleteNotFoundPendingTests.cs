using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.ChapterAssociates;

/// <summary>
/// PENDING: Deleting a chapter-officer association that does not exist should return
/// 404. Today it returns 200 OK (best-effort delete). Either behavior can be correct
/// — but we should match the rest of the API's convention. Fails today; will pass
/// once the endpoint checks existence before calling repository delete.
/// See: code-quality-todos.md "Endpoint behavior review".
/// </summary>
public class ChapterOfficerDeleteNotFoundPendingTests : IntegrationTestBase {
    [Test]
    public async Task Delete_nonexistent_officer_should_return_404() {
        var chapter = Builder.SeedChapter();
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditOfficers } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        // Construct a DELETE with a JSON body — the endpoint reads MemberId+ChapterId
        // from the request body for DELETE /api/chapterofficers.
        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/chapterofficers") {
            Content = JsonContent.Create(new { MemberId = Guid.NewGuid(), ChapterId = chapter.Id })
        };
        var response = await client.SendAsync(request);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}
