using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LinqToDB;
using Microsoft.Extensions.Logging.Abstractions;
using Quartermaster.Data;
using Quartermaster.Data.Options;
using Quartermaster.Server.Messaging;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Messaging;

public class TelegramMessageChannelTests : RepositoryTestBase {
    private DbContext _context = default!;
    private OptionRepository _optionRepo = default!;
    private RecordingHandler _handler = default!;

    [Before(Test)]
    public void Setup() {
        _context = Db;
        _optionRepo = new OptionRepository(_context, AuditLog);
        _handler = new RecordingHandler();
    }

    private void SetBotToken(string? token) {
        _context.SystemOptions.Where(o => o.Identifier == "messaging.telegram.bot_token").Delete();
        if (token != null) {
            _context.Insert(new SystemOption { Identifier = "messaging.telegram.bot_token", Value = token });
        }
    }

    private TelegramMessageChannel Build() {
        var factory = new StubHttpClientFactory(_handler);
        return new TelegramMessageChannel(factory, _optionRepo, NullLogger<TelegramMessageChannel>.Instance);
    }

    [Test]
    public async Task IsConfigured_false_when_token_missing() {
        SetBotToken(null);
        await Assert.That(Build().IsConfigured).IsFalse();
    }

    [Test]
    public async Task IsConfigured_true_when_token_set() {
        SetBotToken("1234:ABC");
        await Assert.That(Build().IsConfigured).IsTrue();
    }

    [Test]
    public async Task Send_fails_when_token_missing() {
        SetBotToken(null);
        var result = await Build().SendAsync(new ChannelMessage("123", "S", "B"));
        await Assert.That(result.Accepted).IsFalse();
        await Assert.That(_handler.Requests.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Send_fails_when_chat_id_empty() {
        SetBotToken("token");
        var result = await Build().SendAsync(new ChannelMessage("", "S", "B"));
        await Assert.That(result.Accepted).IsFalse();
        await Assert.That(_handler.Requests.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Send_posts_to_telegram_with_expected_payload() {
        SetBotToken("12345:ABC");
        _handler.Respond(HttpStatusCode.OK, """{"ok":true,"result":{"message_id":1}}""");

        var result = await Build().SendAsync(new ChannelMessage(
            ChannelAddress: "987654",
            Subject: "Neuer Antrag",
            Body: "Es wurde ein neuer Antrag eingereicht."));

        await Assert.That(result.Accepted).IsTrue();
        await Assert.That(_handler.Requests.Count).IsEqualTo(1);
        var req = _handler.Requests[0];
        await Assert.That(req.RequestUri!.ToString()).IsEqualTo("https://api.telegram.org/bot12345:ABC/sendMessage");
        await Assert.That(req.Method).IsEqualTo(HttpMethod.Post);

        var bodyJson = await req.Content!.ReadAsStringAsync();
        var body = JsonDocument.Parse(bodyJson).RootElement;
        await Assert.That(body.GetProperty("chat_id").GetString()).IsEqualTo("987654");
        await Assert.That(body.GetProperty("parse_mode").GetString()).IsEqualTo("Markdown");
        var text = body.GetProperty("text").GetString();
        await Assert.That(text!.Contains("*Neuer Antrag*")).IsTrue();
        await Assert.That(text!.Contains("Es wurde ein neuer Antrag eingereicht.")).IsTrue();
    }

    [Test]
    public async Task Send_returns_fail_on_non_2xx_from_telegram() {
        SetBotToken("token");
        _handler.Respond(HttpStatusCode.Unauthorized, """{"ok":false,"error_code":401,"description":"Unauthorized"}""");

        var result = await Build().SendAsync(new ChannelMessage("123", "S", "B"));
        await Assert.That(result.Accepted).IsFalse();
        await Assert.That(result.Error).IsNotNull();
    }

    [Test]
    public async Task Send_returns_fail_when_telegram_responds_ok_false() {
        SetBotToken("token");
        _handler.Respond(HttpStatusCode.OK, """{"ok":false,"description":"chat not found"}""");

        var result = await Build().SendAsync(new ChannelMessage("123", "S", "B"));
        await Assert.That(result.Accepted).IsFalse();
        await Assert.That(result.Error!.Contains("chat not found")).IsTrue();
    }

    [Test]
    public async Task Send_returns_fail_on_http_request_exception() {
        SetBotToken("token");
        _handler.ThrowOnNext = new HttpRequestException("network down");

        var result = await Build().SendAsync(new ChannelMessage("123", "S", "B"));
        await Assert.That(result.Accepted).IsFalse();
        await Assert.That(result.Error!.Contains("network down")).IsTrue();
    }

    private class StubHttpClientFactory : IHttpClientFactory {
        private readonly HttpMessageHandler _handler;
        public StubHttpClientFactory(HttpMessageHandler handler) {
            _handler = handler;
        }
        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private class RecordingHandler : HttpMessageHandler {
        public List<HttpRequestMessage> Requests { get; } = new();
        public HttpStatusCode NextStatus { get; private set; } = HttpStatusCode.OK;
        public string NextBody { get; private set; } = """{"ok":true,"result":{}}""";
        public HttpRequestException? ThrowOnNext { get; set; }

        public void Respond(HttpStatusCode status, string body) {
            NextStatus = status;
            NextBody = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) {
            Requests.Add(request);
            if (ThrowOnNext != null) {
                var ex = ThrowOnNext;
                ThrowOnNext = null;
                throw ex;
            }
            var response = new HttpResponseMessage(NextStatus) {
                Content = new StringContent(NextBody, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
