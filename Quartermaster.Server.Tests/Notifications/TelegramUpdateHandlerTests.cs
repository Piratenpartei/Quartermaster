using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LinqToDB;
using Microsoft.Extensions.Logging.Abstractions;
using Quartermaster.Data.Notifications;
using Quartermaster.Server.Notifications.Telegram;
using Quartermaster.Server.Tests.Infrastructure;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Quartermaster.Server.Tests.Notifications;

/// <summary>
/// Exercises <see cref="TelegramUpdateHandler"/> with synthetic <see cref="Update"/>
/// objects. The handler's outbound replies go through a recording HttpMessageHandler
/// wrapped in a real <see cref="TelegramBotClient"/>, so the test sees the URL the
/// package built without us mocking the entire client interface.
/// </summary>
public class TelegramUpdateHandlerTests : RepositoryTestBase {
    private TelegramLinkTokenRepository _tokenRepo = default!;
    private TelegramUpdateHandler _handler = default!;
    private TestDataBuilder _builder = default!;
    private RecordingHandler _httpHandler = default!;
    private ITelegramBotClient _bot = default!;

    [Before(Test)]
    public void Setup() {
        _tokenRepo = new TelegramLinkTokenRepository(Db);
        _handler = new TelegramUpdateHandler(_tokenRepo, NullLogger<TelegramUpdateHandler>.Instance);
        _builder = new TestDataBuilder(Db);
        _httpHandler = new RecordingHandler();
        _bot = new TelegramBotClient("12345:AABBCCDDEEFFGGHHIIJJKKLLMMNNOOPPQQR", new HttpClient(_httpHandler));
    }

    private static Update StartUpdate(long chatId, string text) {
        return new Update {
            Id = 1,
            Message = new Message {
                Id = 1,
                Date = DateTime.UtcNow,
                Chat = new Chat { Id = chatId, Type = Telegram.Bot.Types.Enums.ChatType.Private },
                Text = text
            }
        };
    }

    [Test]
    public async Task Link_with_valid_token_links_user_and_replies() {
        var user = _builder.SeedUser();
        var now = new DateTime(2026, 5, 27, 12, 0, 0, DateTimeKind.Utc);
        var token = _tokenRepo.Create(user.Id, now);

        await _handler.HandleAsync(_bot, StartUpdate(555, $"/link {token.Token}"), now, CancellationToken.None);

        var updatedUser = Db.Users.Single(u => u.Id == user.Id);
        await Assert.That(updatedUser.TelegramChatId).IsEqualTo("555");
        await Assert.That(_httpHandler.Requests.Count).IsEqualTo(1);
        await Assert.That(_httpHandler.Requests[0].RequestUri!.AbsoluteUri.Contains("/sendMessage")).IsTrue();
    }

    [Test]
    public async Task Link_with_unknown_token_does_not_link_user() {
        var user = _builder.SeedUser();
        var now = new DateTime(2026, 5, 27, 12, 0, 0, DateTimeKind.Utc);

        await _handler.HandleAsync(_bot, StartUpdate(555, "/link bogustoken"), now, CancellationToken.None);

        var updatedUser = Db.Users.Single(u => u.Id == user.Id);
        await Assert.That(updatedUser.TelegramChatId).IsNull();
        await Assert.That(_httpHandler.Requests.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Link_without_token_replies_with_help() {
        var now = new DateTime(2026, 5, 27, 12, 0, 0, DateTimeKind.Utc);

        await _handler.HandleAsync(_bot, StartUpdate(555, "/link"), now, CancellationToken.None);

        await Assert.That(_httpHandler.Requests.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Start_replies_with_welcome_and_does_not_link() {
        var user = _builder.SeedUser();
        var now = new DateTime(2026, 5, 27, 12, 0, 0, DateTimeKind.Utc);
        var token = _tokenRepo.Create(user.Id, now);

        await _handler.HandleAsync(_bot, StartUpdate(555, $"/start {token.Token}"), now, CancellationToken.None);

        var updatedUser = Db.Users.Single(u => u.Id == user.Id);
        await Assert.That(updatedUser.TelegramChatId).IsNull();
        await Assert.That(_httpHandler.Requests.Count).IsEqualTo(1);
        var unconsumedToken = _tokenRepo.Get(token.Token);
        await Assert.That(unconsumedToken!.ConsumedAt).IsNull();
    }

    [Test]
    public async Task Non_command_message_replies_with_hint() {
        var now = new DateTime(2026, 5, 27, 12, 0, 0, DateTimeKind.Utc);

        await _handler.HandleAsync(_bot, StartUpdate(555, "hello there"), now, CancellationToken.None);

        await Assert.That(_httpHandler.Requests.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Non_message_update_is_ignored() {
        var now = new DateTime(2026, 5, 27, 12, 0, 0, DateTimeKind.Utc);
        var update = new Update { Id = 5, Message = null };
        await _handler.HandleAsync(_bot, update, now, CancellationToken.None);
        await Assert.That(_httpHandler.Requests.Count).IsEqualTo(0);
    }

    private class RecordingHandler : HttpMessageHandler {
        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) {
            Requests.Add(request);
            var response = new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent(
                    """{"ok":true,"result":{"message_id":1,"date":1,"chat":{"id":555,"type":"private"}}}""",
                    Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
