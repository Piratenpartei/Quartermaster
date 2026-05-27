using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api.Notifications;
using Quartermaster.Data.Notifications;
using Quartermaster.Data.Options;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Notifications;

public class TelegramLinkEndpointsTests : IntegrationTestBase {
    [Test]
    public async Task GET_status_returns_401_when_anonymous() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/users/telegram-link");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GET_status_returns_unlinked_by_default() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);
        var dto = await client.GetFromJsonAsync<TelegramLinkStatusDTO>("/api/users/telegram-link");
        await Assert.That(dto!.Linked).IsFalse();
        await Assert.That(dto.ChatId).IsNull();
    }

    [Test]
    public async Task GET_status_returns_linked_after_chat_id_is_set() {
        var (user, token) = Builder.SeedAuthenticatedUser();
        Db.Users.Where(u => u.Id == user.Id).Set(u => u.TelegramChatId, "12345").Update();

        using var client = AuthenticatedClient(token);
        var dto = await client.GetFromJsonAsync<TelegramLinkStatusDTO>("/api/users/telegram-link");
        await Assert.That(dto!.Linked).IsTrue();
        await Assert.That(dto.ChatId).IsEqualTo("12345");
    }

    [Test]
    public async Task POST_creates_token_and_returns_deeplink_when_bot_username_set() {
        var (user, token) = Builder.SeedAuthenticatedUser();
        Db.Insert(new SystemOption { Identifier = "messaging.telegram.bot_username", Value = "QuartermasterBot" });

        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsync("/api/users/telegram-link", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<TelegramLinkStartDTO>();

        await Assert.That(dto!.Token.Length).IsGreaterThan(20);
        await Assert.That(dto.Deeplink).IsEqualTo("https://t.me/QuartermasterBot");
        await Assert.That(dto.BotUsername).IsEqualTo("QuartermasterBot");
        await Assert.That(dto.ExpiresAt > DateTime.UtcNow).IsTrue();

        var row = Db.TelegramLinkTokens.Single(t => t.Token == dto.Token);
        await Assert.That(row.UserId).IsEqualTo(user.Id);
    }

    [Test]
    public async Task POST_returns_null_deeplink_when_bot_username_missing() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        Db.SystemOptions.Where(o => o.Identifier == "messaging.telegram.bot_username").Delete();

        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsync("/api/users/telegram-link", null);
        var dto = await response.Content.ReadFromJsonAsync<TelegramLinkStartDTO>();

        await Assert.That(dto!.Deeplink).IsNull();
        await Assert.That(dto.Token.Length).IsGreaterThan(20);
    }

    [Test]
    public async Task DELETE_clears_chat_id_and_revokes_unconsumed_tokens() {
        var (user, token) = Builder.SeedAuthenticatedUser();
        Db.Users.Where(u => u.Id == user.Id).Set(u => u.TelegramChatId, "999").Update();
        Db.Insert(new TelegramLinkToken {
            Token = "abc123", UserId = user.Id,
            CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        });

        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.DeleteAsync("/api/users/telegram-link");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        var updatedUser = Db.Users.Single(u => u.Id == user.Id);
        await Assert.That(updatedUser.TelegramChatId).IsNull();
        var leftover = Db.TelegramLinkTokens.Where(t => t.UserId == user.Id).ToList();
        await Assert.That(leftover.Count).IsEqualTo(0);
    }
}
