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
using Quartermaster.Data;
using Quartermaster.Data.Notifications;
using Quartermaster.Server.Messaging;
using Quartermaster.Server.Notifications.Telegram;
using Quartermaster.Server.Tests.Infrastructure;
using Telegram.Bot;

namespace Quartermaster.Server.Tests.Messaging;

public class TelegramMessageChannelTests : RepositoryTestBase {
    private const string SentBody = """{"ok":true,"result":{"message_id":1,"date":1,"chat":{"id":987654,"type":"private"}}}""";

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
        return new TelegramMessageChannel(_factory, _logRepo, NullLogger<TelegramMessageChannel>.Instance) {
            RetryDelays = [TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero]
        };
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
        _handler.Respond(HttpStatusCode.OK, SentBody);

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
    public async Task Send_marks_log_failed_when_every_attempt_throws() {
        _factory.BotToken = "12345:ABC";
        _handler.Throw(new HttpRequestException("network down"));

        var result = await Build().SendAsync(new ChannelMessage("987654", "S", "B"));

        await Assert.That(result.Accepted).IsFalse();
        await Assert.That(_handler.Requests.Count).IsEqualTo(4);
        var log = Db.NotificationLogs.Where(l => l.ChannelId == "telegram").Single();
        await Assert.That(log.Status).IsEqualTo("Failed");
        await Assert.That(log.AttemptCount).IsEqualTo(4);
        await Assert.That(log.Error!.Contains("network down")).IsTrue();
    }

    [Test]
    public async Task Send_retries_after_a_timeout_and_marks_sent() {
        _factory.BotToken = "12345:ABC";
        _handler.Throw(new TaskCanceledException("timed out"));
        _handler.Respond(HttpStatusCode.OK, SentBody);

        var result = await Build().SendAsync(new ChannelMessage("987654", "S", "B"));

        await Assert.That(result.Accepted).IsTrue();
        await Assert.That(_handler.Requests.Count).IsEqualTo(2);
        var log = Db.NotificationLogs.Where(l => l.ChannelId == "telegram").Single();
        await Assert.That(log.Status).IsEqualTo("Sent");
        await Assert.That(log.AttemptCount).IsEqualTo(2);
    }

    [Test]
    public async Task Send_retries_server_errors() {
        _factory.BotToken = "12345:ABC";
        _handler.Respond(HttpStatusCode.BadGateway, """{"ok":false,"error_code":502,"description":"Bad Gateway"}""");
        _handler.Respond(HttpStatusCode.OK, SentBody);

        var result = await Build().SendAsync(new ChannelMessage("987654", "S", "B"));

        await Assert.That(result.Accepted).IsTrue();
        await Assert.That(_handler.Requests.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Send_does_not_retry_a_rejected_message() {
        _factory.BotToken = "12345:ABC";
        _handler.Respond(HttpStatusCode.BadRequest, """{"ok":false,"error_code":400,"description":"Bad Request: can't parse entities"}""");

        var result = await Build().SendAsync(new ChannelMessage("987654", "S", "B"));

        await Assert.That(result.Accepted).IsFalse();
        await Assert.That(_handler.Requests.Count).IsEqualTo(1);
        var log = Db.NotificationLogs.Where(l => l.ChannelId == "telegram").Single();
        await Assert.That(log.Status).IsEqualTo("Failed");
        await Assert.That(log.AttemptCount).IsEqualTo(1);
    }

    [Test]
    public async Task Send_waits_for_retry_after_on_too_many_requests() {
        _factory.BotToken = "12345:ABC";
        _handler.Respond((HttpStatusCode)429, """{"ok":false,"error_code":429,"description":"Too Many Requests: retry after 1","parameters":{"retry_after":1}}""");
        _handler.Respond(HttpStatusCode.OK, SentBody);

        var result = await Build().SendAsync(new ChannelMessage("987654", "S", "B"));

        await Assert.That(result.Accepted).IsTrue();
        await Assert.That(_handler.Requests.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Send_gives_up_when_retry_after_is_too_long() {
        _factory.BotToken = "12345:ABC";
        _handler.Respond((HttpStatusCode)429, """{"ok":false,"error_code":429,"description":"Too Many Requests: retry after 3600","parameters":{"retry_after":3600}}""");

        var result = await Build().SendAsync(new ChannelMessage("987654", "S", "B"));

        await Assert.That(result.Accepted).IsFalse();
        await Assert.That(_handler.Requests.Count).IsEqualTo(1);
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
        private readonly List<Func<HttpResponseMessage>> _steps = new();

        /// <summary>Queues a response; once the queue is used up, the last step repeats.</summary>
        public void Respond(HttpStatusCode status, string body) {
            _steps.Add(() => new HttpResponseMessage(status) {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }

        public void Throw(Exception ex) {
            _steps.Add(() => throw ex);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) {
            Requests.Add(request);
            if (_steps.Count == 0)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"ok":true,"result":{}}""", Encoding.UTF8, "application/json") });

            var step = _steps[Math.Min(Requests.Count - 1, _steps.Count - 1)];
            return Task.FromResult(step());
        }
    }
}
