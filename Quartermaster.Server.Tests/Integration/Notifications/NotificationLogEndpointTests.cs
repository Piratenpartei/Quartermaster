using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.Notifications;
using Quartermaster.Data.Notifications;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Notifications;

public class NotificationLogEndpointTests : IntegrationTestBase {
    private void SeedLog(string recipient, string? sourceType = null, Guid? sourceId = null) {
        Db.Insert(new NotificationLog {
            Id = Guid.NewGuid(),
            ChannelId = "smtp",
            Recipient = recipient,
            Subject = "Test",
            SourceEntityType = sourceType,
            SourceEntityId = sourceId,
            Status = "Sent",
            AttemptCount = 1,
            CreatedAt = DateTime.UtcNow
        });
    }

    [Test]
    public async Task Returns_401_when_anonymous() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/notificationlogs");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_view_notification_logs() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/notificationlogs");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_recent_logs_for_authorized_user() {
        SeedLog("a@test.local");
        SeedLog("b@test.local");
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewNotificationLogs });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/notificationlogs");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<List<NotificationLogDTO>>();
        await Assert.That(list).IsNotNull();
        await Assert.That(list!.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Filters_by_source_entity() {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        SeedLog("a@test.local", "Application", id1);
        SeedLog("b@test.local", "Application", id2);
        SeedLog("c@test.local", "Application", id1);
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewNotificationLogs });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/notificationlogs?SourceEntityType=Application&SourceEntityId={id1}");
        var list = await response.Content.ReadFromJsonAsync<List<NotificationLogDTO>>();
        await Assert.That(list!.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Returns_empty_list_when_no_logs() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewNotificationLogs });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/notificationlogs");
        var list = await response.Content.ReadFromJsonAsync<List<NotificationLogDTO>>();
        await Assert.That(list!.Count).IsEqualTo(0);
    }
}
