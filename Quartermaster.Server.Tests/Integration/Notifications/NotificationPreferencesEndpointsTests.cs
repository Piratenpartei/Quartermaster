using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api.Notifications;
using Quartermaster.Data.Notifications;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Notifications;

public class NotificationPreferencesEndpointsTests : IntegrationTestBase {
    [Test]
    public async Task GET_returns_401_when_anonymous() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/users/notification-preferences");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GET_returns_full_matrix_with_defaults_when_no_overrides() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);

        var response = await client.GetAsync("/api/users/notification-preferences");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var dto = await response.Content.ReadFromJsonAsync<NotificationPreferencesDTO>();
        await Assert.That(dto).IsNotNull();
        await Assert.That(dto!.Triggers.Count).IsGreaterThanOrEqualTo(3);
        await Assert.That(dto.Channels.Count).IsGreaterThanOrEqualTo(3);
        await Assert.That(dto.Cells.Count).IsEqualTo(dto.Triggers.Count * dto.Channels.Count);

        // Defaults: smtp on for everything, others off.
        foreach (var cell in dto.Cells) {
            var expected = cell.ChannelId == "email";
            await Assert.That(cell.Enabled).IsEqualTo(expected);
        }
    }

    [Test]
    public async Task GET_reflects_explicit_overrides() {
        var (user, token) = Builder.SeedAuthenticatedUser();
        Db.Insert(new UserNotificationPreference {
            UserId = user.Id, TriggerId = "motion_submitted", ChannelId = "email", Enabled = false
        });
        using var client = AuthenticatedClient(token);
        var dto = await client.GetFromJsonAsync<NotificationPreferencesDTO>("/api/users/notification-preferences");

        var cell = dto!.Cells.Single(c => c.TriggerId == "motion_submitted" && c.ChannelId == "email");
        await Assert.That(cell.Enabled).IsFalse();
    }

    [Test]
    public async Task PUT_persists_overrides_for_caller_only() {
        var (alice, token) = Builder.SeedAuthenticatedUser(firstName: "Alice");
        var (bob, _) = Builder.SeedAuthenticatedUser(firstName: "Bob");
        Db.Insert(new UserNotificationPreference {
            UserId = bob.Id, TriggerId = "motion_submitted", ChannelId = "email", Enabled = false
        });

        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var req = new UpdateNotificationPreferencesRequest {
            Cells = new() {
                new() { TriggerId = "motion_submitted", ChannelId = "email", Enabled = false },
                new() { TriggerId = "application_submitted", ChannelId = "email", Enabled = true }
            }
        };
        var response = await client.PutAsJsonAsync("/api/users/notification-preferences", req);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        var aliceRows = Db.UserNotificationPreferences.Where(p => p.UserId == alice.Id).ToList();
        await Assert.That(aliceRows.Count).IsEqualTo(2);
        var bobRows = Db.UserNotificationPreferences.Where(p => p.UserId == bob.Id).ToList();
        await Assert.That(bobRows.Count).IsEqualTo(1);
    }

    [Test]
    public async Task PUT_silently_drops_unknown_trigger_or_channel_ids() {
        var (user, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var req = new UpdateNotificationPreferencesRequest {
            Cells = new() {
                new() { TriggerId = "motion_submitted", ChannelId = "email", Enabled = true },
                new() { TriggerId = "made_up_trigger", ChannelId = "email", Enabled = true },
                new() { TriggerId = "motion_submitted", ChannelId = "fictional_channel", Enabled = true }
            }
        };
        await client.PutAsJsonAsync("/api/users/notification-preferences", req);

        var rows = Db.UserNotificationPreferences.Where(p => p.UserId == user.Id).ToList();
        await Assert.That(rows.Count).IsEqualTo(1);
        await Assert.That(rows[0].TriggerId).IsEqualTo("motion_submitted");
    }

    [Test]
    public async Task PUT_replaces_prior_overrides() {
        var (user, token) = Builder.SeedAuthenticatedUser();
        Db.Insert(new UserNotificationPreference {
            UserId = user.Id, TriggerId = "old_trigger_in_db", ChannelId = "email", Enabled = true
        });

        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var req = new UpdateNotificationPreferencesRequest {
            Cells = new() {
                new() { TriggerId = "motion_submitted", ChannelId = "email", Enabled = false }
            }
        };
        await client.PutAsJsonAsync("/api/users/notification-preferences", req);

        var rows = Db.UserNotificationPreferences.Where(p => p.UserId == user.Id).ToList();
        await Assert.That(rows.Count).IsEqualTo(1);
        await Assert.That(rows[0].TriggerId).IsEqualTo("motion_submitted");
    }
}
