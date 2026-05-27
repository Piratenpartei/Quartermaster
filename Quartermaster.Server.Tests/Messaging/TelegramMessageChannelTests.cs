using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LinqToDB;
using Microsoft.Extensions.Logging.Abstractions;
using Quartermaster.Data;
using Quartermaster.Data.Notifications;
using Quartermaster.Server.Messaging;
using Quartermaster.Server.Notifications.Telegram;
using Quartermaster.Server.Tests.Infrastructure;
using Telegram.Bot;

namespace Quartermaster.Server.Tests.Messaging;

public class TelegramMessageChannelTests : RepositoryTestBase {
    private NotificationLogRepository _logRepo = default!;
    private RecordingHandler _handler = default!;
    private StubFactory _factory = default!;

    [Before(Test)]
    public void Setup() {
        _logRepo = new NotificationLogRepository(Db);
        _handler = new RecordingHandler();
        _factory = new StubFactory(_handler);
    }

    private TelegramMessageChannel Build() {
        return new TelegramMessageChannel(_factory, _logRepo, NullLogger<TelegramMessageChannel>.Instance);
    }

    [Test]
    public async Task IsConfigured_false_when_token_missing() {
        _factory.BotToken = null;
        await Assert.That(Build().IsConfigured).IsFalse();
    }

    [Test]
    public async Task IsConfigured_true_when_token_set() {
        _factory.BotToken = "1234:ABC";
        await Assert.That(Build().IsConfigured).IsTrue();
    }

    [Test]
    public async Task Send_fails_when_token_missing() {
        _factory.BotToken = null;
        var result = await Build().SendAsync(new ChannelMessage("123", "S", "B"));
        await Assert.That(result.Accepted).IsFalse();
        await Assert.That(_handler.Requests.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Send_fails_when_chat_id_empty() {
        _factory.BotToken = "12345:AABBCCDDEEFFGGHHIIJJKKLLMMNNOOPPQQR";
        var result = await Build().SendAsync(new ChannelMessage("", "S", "B"));
        await Assert.That(result.Accepted).IsFalse();
        await Assert.That(_handler.Requests.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Send_writes_pending_log_and_marks_sent_on_success() {
        _factory.BotToken = "12345:ABC";
        _handler.Respond(HttpStatusCode.OK, """{"ok":true,"result":{"message_id":1,"date":1,"chat":{"id":987654,"type":"private"}}}""");

        var result = await Build().SendAsync(new ChannelMessage(
            ChannelAddress: "987654",
            Subject: "Neuer Antrag",
            Body: "Es wurde ein neuer Antrag eingereicht."));

        await Assert.That(result.Accepted).IsTrue();
        var logs = Db.NotificationLogs.Where(l => l.ChannelId == "telegram").ToList();
        await Assert.That(logs.Count).IsEqualTo(1);
        await Assert.That(logs[0].Status).IsEqualTo("Sent");
        await Assert.That(logs[0].Recipient).IsEqualTo("987654");
        await Assert.That(logs[0].Subject).IsEqualTo("Neuer Antrag");
    }

    [Test]
    public async Task Send_marks_log_failed_when_send_throws() {
        _factory.BotToken = "12345:ABC";
        _handler.ThrowOnNext = new HttpRequestException("network down");

        var result = await Build().SendAsync(new ChannelMessage("987654", "S", "B"));

        await Assert.That(result.Accepted).IsFalse();
        var log = Db.NotificationLogs.Where(l => l.ChannelId == "telegram").Single();
        await Assert.That(log.Status).IsEqualTo("Failed");
        await Assert.That(log.Error!.Contains("network down")).IsTrue();
    }

    private class StubFactory : TelegramBotClientFactory {
        public string? BotToken { get; set; }
        public string? BotUsername { get; set; }
        private readonly RecordingHandler _handler;

        public StubFactory(RecordingHandler handler) : base(null!, null!) {
            // Both base ctor args are unused — overrides bypass option lookup and cache entirely.
            _handler = handler;
        }

        public override ITelegramBotClient? CreateOrNull() {
            if (string.IsNullOrEmpty(BotToken)) {
                return null;
            }
            return new TelegramBotClient(BotToken, new HttpClient(_handler));
        }

        public override string? GetBotUsername() => BotUsername;
    }

    private class RecordingHandler : HttpMessageHandler {
        public List<HttpRequestMessage> Requests { get; } = new();
        private HttpStatusCode _nextStatus = HttpStatusCode.OK;
        private string _nextBody = """{"ok":true,"result":{}}""";
        public HttpRequestException? ThrowOnNext { get; set; }

        public void Respond(HttpStatusCode status, string body) {
            _nextStatus = status;
            _nextBody = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) {
            Requests.Add(request);
            if (ThrowOnNext != null) {
                var ex = ThrowOnNext;
                ThrowOnNext = null;
                throw ex;
            }
            var response = new HttpResponseMessage(_nextStatus) {
                Content = new StringContent(_nextBody, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
