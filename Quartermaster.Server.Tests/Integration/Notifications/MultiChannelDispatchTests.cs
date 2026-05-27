using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LinqToDB;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Quartermaster.Api;
using Quartermaster.Api.Motions;
using Quartermaster.Data.Notifications;
using Quartermaster.Data.Options;
using Quartermaster.Server.Notifications.Telegram;
using Quartermaster.Server.Tests.Infrastructure;
using Telegram.Bot;

namespace Quartermaster.Server.Tests.Integration.Notifications;

/// <summary>
/// Phase 4 — verifies the dispatcher fans out to telegram once the channel is configured
/// AND the recipient is linked AND opted in. Uses a per-test host with a stub
/// <see cref="TelegramBotClientFactory"/> so we never call api.telegram.org.
/// </summary>
public class MultiChannelDispatchTests : IntegrationTestBase {
    private async Task SubmitMotion(HttpClient client, Guid chapterId, string title) {
        await AttachAntiforgeryTokenAsync(client);
        await client.PostAsJsonAsync("/api/motions", new MotionCreateRequest {
            ChapterId = chapterId,
            AuthorName = "Anonymous Author",
            AuthorEmail = "author@example.com",
            Title = title,
            Text = "Body"
        });
    }

    private WebApplicationFactory<Program> HostWithStubBot() {
        return Factory.WithWebHostBuilder(b => {
            b.ConfigureServices(services => {
                var existing = services.Where(s => s.ServiceType == typeof(TelegramBotClientFactory)).ToList();
                foreach (var d in existing) {
                    services.Remove(d);
                }
                services.AddScoped<TelegramBotClientFactory, StubBotClientFactory>();
            });
        });
    }

    [Test]
    public async Task Telegram_log_row_not_written_when_bot_token_missing() {
        var chapter = Builder.SeedChapter("C");
        var (user, _) = Builder.SeedAuthenticatedUser(
            email: "user@test.local",
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMotions } });
        Db.Users.Where(u => u.Id == user.Id).Set(u => u.TelegramChatId, "555").Update();
        Db.Insert(new UserNotificationPreference {
            UserId = user.Id, TriggerId = "motion_submitted", ChannelId = "telegram", Enabled = true
        });

        using var client = AnonymousClient();
        await SubmitMotion(client, chapter.Id, "Without Bot");

        var telegramLogs = Db.NotificationLogs.Where(l => l.ChannelId == "telegram").ToList();
        await Assert.That(telegramLogs.Count).IsEqualTo(0);
        var smtpLogs = Db.NotificationLogs.Where(l => l.ChannelId == "email").ToList();
        await Assert.That(smtpLogs.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Telegram_log_row_not_written_when_user_chat_id_missing() {
        var chapter = Builder.SeedChapter("C");
        var (user, _) = Builder.SeedAuthenticatedUser(
            email: "user@test.local",
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMotions } });
        Db.Insert(new SystemOption { Identifier = "messaging.telegram.bot_token", Value = "12345:ABC" });
        Db.Insert(new UserNotificationPreference {
            UserId = user.Id, TriggerId = "motion_submitted", ChannelId = "telegram", Enabled = true
        });

        using var host = HostWithStubBot();
        using var client = host.CreateClient();
        await SubmitMotion(client, chapter.Id, "Without ChatId");

        var telegramLogs = Db.NotificationLogs.Where(l => l.ChannelId == "telegram").ToList();
        await Assert.That(telegramLogs.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Telegram_log_row_written_when_bot_configured_and_user_linked_and_opted_in() {
        var chapter = Builder.SeedChapter("C");
        var (user, _) = Builder.SeedAuthenticatedUser(
            email: "user@test.local",
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMotions } });
        Db.Users.Where(u => u.Id == user.Id).Set(u => u.TelegramChatId, "555").Update();
        Db.Insert(new SystemOption { Identifier = "messaging.telegram.bot_token", Value = "12345:ABC" });
        Db.Insert(new UserNotificationPreference {
            UserId = user.Id, TriggerId = "motion_submitted", ChannelId = "telegram", Enabled = true
        });

        using var host = HostWithStubBot();
        using var client = host.CreateClient();
        await SubmitMotion(client, chapter.Id, "Routed To Telegram");

        var telegramLogs = Db.NotificationLogs.Where(l => l.ChannelId == "telegram").ToList();
        await Assert.That(telegramLogs.Count).IsEqualTo(1);
        await Assert.That(telegramLogs[0].RecipientUserId).IsEqualTo(user.Id);
        await Assert.That(telegramLogs[0].Recipient).IsEqualTo("555");
        await Assert.That(telegramLogs[0].Status).IsEqualTo("Sent");
    }

    [Test]
    public async Task Telegram_body_contains_deeplink_built_from_globals_base_url() {
        var chapter = Builder.SeedChapter("Chapter X");
        var (user, _) = Builder.SeedAuthenticatedUser(
            email: "user@test.local",
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMotions } });
        Db.Users.Where(u => u.Id == user.Id).Set(u => u.TelegramChatId, "555").Update();
        Db.Insert(new SystemOption { Identifier = "messaging.telegram.bot_token", Value = "12345:ABC" });
        Db.Insert(new SystemOption { Identifier = "system.public_base_url", Value = "https://qm.test.local" });
        Db.Insert(new UserNotificationPreference {
            UserId = user.Id, TriggerId = "motion_submitted", ChannelId = "telegram", Enabled = true
        });

        using var host = HostWithStubBot();
        using var client = host.CreateClient();
        await SubmitMotion(client, chapter.Id, "Linked Motion");

        var motion = Db.Motions.Single(m => m.Title == "Linked Motion");
        var log = Db.NotificationLogs.Single(l => l.ChannelId == "telegram");
        await Assert.That(log.Body!.Contains($"https://qm.test.local/Administration/Motions/{motion.Id}")).IsTrue();
    }

    [Test]
    public async Task Telegram_body_is_raw_markdown_not_html() {
        var chapter = Builder.SeedChapter("Chapter X");
        var (user, _) = Builder.SeedAuthenticatedUser(
            email: "user@test.local",
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMotions } });
        Db.Users.Where(u => u.Id == user.Id).Set(u => u.TelegramChatId, "555").Update();
        Db.Insert(new SystemOption { Identifier = "messaging.telegram.bot_token", Value = "12345:ABC" });
        Db.Insert(new UserNotificationPreference {
            UserId = user.Id, TriggerId = "motion_submitted", ChannelId = "telegram", Enabled = true
        });

        using var host = HostWithStubBot();
        using var client = host.CreateClient();
        await SubmitMotion(client, chapter.Id, "Markdown Motion");

        var log = Db.NotificationLogs.Single(l => l.ChannelId == "telegram");
        await Assert.That(log.Body!.Contains("<p>")).IsFalse();
        await Assert.That(log.Body!.Contains("<em>")).IsFalse();
        await Assert.That(log.Body!.Contains("*Chapter X*")).IsTrue();
        await Assert.That(log.Body!.Contains("*Markdown Motion*")).IsTrue();
    }

    [Test]
    public async Task Telegram_skipped_when_user_opted_out_even_if_linked() {
        var chapter = Builder.SeedChapter("C");
        var (user, _) = Builder.SeedAuthenticatedUser(
            email: "user@test.local",
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMotions } });
        Db.Users.Where(u => u.Id == user.Id).Set(u => u.TelegramChatId, "555").Update();
        Db.Insert(new SystemOption { Identifier = "messaging.telegram.bot_token", Value = "12345:ABC" });
        // Default for telegram is OFF — no override needed.

        using var host = HostWithStubBot();
        using var client = host.CreateClient();
        await SubmitMotion(client, chapter.Id, "Opted Out");

        var telegramLogs = Db.NotificationLogs.Where(l => l.ChannelId == "telegram").ToList();
        await Assert.That(telegramLogs.Count).IsEqualTo(0);
    }

    private class StubBotClientFactory : TelegramBotClientFactory {
        public StubBotClientFactory(OptionRepository options, TelegramBotClientCache cache)
            : base(options, cache) { }

        public override ITelegramBotClient? CreateOrNull() {
            var real = base.CreateOrNull();
            if (real == null) {
                return null;
            }
            return new TelegramBotClient("12345:AABBCCDDEEFFGGHHIIJJKKLLMMNNOOPPQQR", new HttpClient(new StubHandler()));
        }
    }

    private class StubHandler : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent(
                    """{"ok":true,"result":{"message_id":1,"date":1,"chat":{"id":555,"type":"private"}}}""",
                    Encoding.UTF8, "application/json")
            });
        }
    }
}
