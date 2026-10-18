using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Quartermaster.Api;
using Quartermaster.Data.Options;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Options;

public class OptionOverrideDeleteEndpointTests : IntegrationTestBase {
    private const string MotionKey = "public_forms.motion.enabled";

    private void SetOverride(Guid chapterId, string value) {
        using var scope = Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<OptionRepository>().SetValue(MotionKey, chapterId, value);
    }

    private SystemOption? Override(Guid chapterId) {
        using var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<OptionRepository>().GetChapterValue(MotionKey, chapterId);
    }

    [Test]
    public async Task Returns_401_when_anonymous() {
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.DeleteAsync($"/api/options/{MotionKey}/overrides/{Guid.NewGuid()}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_without_edit_options() {
        var chapter = Builder.SeedChapter("Chapter");
        SetOverride(chapter.Id, "false");
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: [PermissionIdentifier.ViewOptions]);
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.DeleteAsync($"/api/options/{MotionKey}/overrides/{chapter.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(Override(chapter.Id)).IsNotNull();
    }

    [Test]
    public async Task Returns_404_when_the_chapter_has_no_override() {
        var chapter = Builder.SeedChapter("Chapter");
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: [PermissionIdentifier.EditOptions]);
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.DeleteAsync($"/api/options/{MotionKey}/overrides/{chapter.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Removes_only_that_override_and_logs_it() {
        var chapter = Builder.SeedChapter("Chapter");
        var other = Builder.SeedChapter("Other");
        SetOverride(chapter.Id, "false");
        SetOverride(other.Id, "false");
        var removedId = Override(chapter.Id)!.Id;
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: [PermissionIdentifier.EditOptions]);
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.DeleteAsync($"/api/options/{MotionKey}/overrides/{chapter.Id}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Override(chapter.Id)).IsNull();
        await Assert.That(Override(other.Id)).IsNotNull();
        await Assert.That(Db.AuditLogs.Any(a => a.EntityId == removedId && a.Action == "Deleted")).IsTrue();
    }
}
