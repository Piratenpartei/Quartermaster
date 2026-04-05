using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Data.Events;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Events;

/// <summary>
/// PENDING: Encodes the desired idempotence behavior for checking a completed Text
/// checklist item. Today the endpoint returns 400 when the item is already completed,
/// regardless of item type. For <c>Text</c> items (no side effects) re-checking should
/// be a safe no-op. For <c>CreateMotion</c> / <c>SendEmail</c> the 400 is correct
/// because those perform irreversible side effects.
/// Fails today; will pass once the endpoint treats already-completed Text items
/// as a no-op (200 OK).
/// See: code-quality-todos.md "Endpoint behavior review".
/// </summary>
public class ChecklistItemCheckIdempotencePendingTests : IntegrationTestBase {
    [Test]
    public async Task Checking_already_completed_Text_item_should_be_idempotent_noop() {
        var chapter = Builder.SeedChapter();
        var ev = Builder.SeedEvent(chapter.Id);
        var item = Builder.SeedChecklistItem(ev.Id, label: "Text item",
            itemType: ChecklistItemType.Text, isCompleted: true);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditEvents } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync(
            $"/api/events/{ev.Id}/checklist/{item.Id}/check", new { });
        // Text items have no side effects — re-checking should be a no-op, not a 400.
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Checking_already_completed_CreateMotion_item_should_still_reject_with_400() {
        // Regression guard — irreversible side-effect items must NOT become idempotent.
        var chapter = Builder.SeedChapter();
        var ev = Builder.SeedEvent(chapter.Id);
        var item = Builder.SeedChecklistItem(ev.Id, label: "Creates motion",
            itemType: ChecklistItemType.CreateMotion, isCompleted: true);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditEvents } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync(
            $"/api/events/{ev.Id}/checklist/{item.Id}/check", new { });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }
}
