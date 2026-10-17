using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Quartermaster.Server.AssemblyAssistant.OpenSlides;
using Quartermaster.Server.AssemblyMotions.OpenSlides;
using Quartermaster.Server.Tests.AssemblyAssistant.Fakes;
using Quartermaster.Server.Tests.AssemblyMotions.Fakes;

namespace Quartermaster.Server.Tests.AssemblyAssistant;

[NotInParallel("FakeOpenSlidesClient")]
public class OpenSlidesAutoupdateClientTests {
    private static readonly OpenSlidesSettings Settings = new("https://openslides.invalid", "bot", "secret");
    private static readonly OpenSlidesAutoupdateTimings FastTimings = new(TimeSpan.FromSeconds(2), [TimeSpan.FromMilliseconds(20)]);

    private StreamingHandler _handler = default!;
    private OpenSlidesAutoupdateClient _client = default!;
    private Channel<OpenSlidesMeetingView> _views = default!;

    [Before(Test)]
    public void Setup() {
        FakeOpenSlidesClient.Reset();
        _handler = new StreamingHandler();
        _client = new OpenSlidesAutoupdateClient(new HttpClient(_handler), new FakeOpenSlidesClient(), NullLogger<OpenSlidesAutoupdateClient>.Instance, FastTimings);
        _views = Channel.CreateUnbounded<OpenSlidesMeetingView>();
        _client.Changed += view => _views.Writer.TryWrite(view);
    }

    [After(Test)]
    public async Task Teardown() {
        await _client.DisposeAsync();
    }

    private async Task<OpenSlidesMeetingView> NextViewAsync(Func<OpenSlidesMeetingView, bool> predicate) {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (true) {
            var view = await _views.Reader.ReadAsync(timeout.Token);
            if (predicate(view))
                return view;
        }
    }

    private static string Compact(string json) {
        return string.Join("", json.Split('\n').Select(l => l.Trim()));
    }

    [Test]
    public async Task Stream_applies_lines_in_order() {
        _handler.Enqueue(HttpStatusCode.OK, [Compact(OpenSlidesFixtures.FullMap), """{"poll/8/state": "finished"}""", "{}"], keepOpen: true);

        await _client.StartAsync(Settings, OpenSlidesFixtures.MeetingId, CancellationToken.None);

        var first = await NextViewAsync(v => v.Polls.Count == 2);
        var second = await NextViewAsync(v => v.Polls.Single(p => p.Id == 8).State == "finished");
        await Assert.That(first.Polls.Single(p => p.Id == 8).State).IsEqualTo("started");
        await Assert.That(second.AgendaItems.Count).IsEqualTo(2);
        var request = _handler.Requests.First();
        await Assert.That(request.Url).IsEqualTo("https://openslides.invalid/system/autoupdate");
        await Assert.That(request.Headers["Authentication"]).IsEqualTo("bearer fake");
        await Assert.That(request.Headers["Cookie"]).IsEqualTo("refreshId=refresh-fake");
    }

    [Test]
    public async Task Drop_reconnects_with_reported_token() {
        _handler.Enqueue(HttpStatusCode.OK, [Compact(OpenSlidesFixtures.FullMap)], keepOpen: false, authentication: "bearer refreshed");
        _handler.Enqueue(HttpStatusCode.OK, [Compact(OpenSlidesFixtures.FullMap)], keepOpen: true);

        await _client.StartAsync(Settings, OpenSlidesFixtures.MeetingId, CancellationToken.None);
        await _handler.WaitForRequestsAsync(2);

        await Assert.That(FakeOpenSlidesClient.ReportedTokens).Contains("bearer refreshed");
        await Assert.That(_handler.Requests[1].Headers["Authentication"]).IsEqualTo("bearer refreshed");
    }

    [Test]
    public async Task Unauthorized_logs_in_again() {
        _handler.Enqueue(HttpStatusCode.Unauthorized, [], keepOpen: false);
        _handler.Enqueue(HttpStatusCode.OK, [Compact(OpenSlidesFixtures.FullMap)], keepOpen: true);
        var connected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _client.ConnectionChanged += (isConnected, _) => {
            if (isConnected)
                connected.TrySetResult(true);
        };

        await _client.StartAsync(Settings, OpenSlidesFixtures.MeetingId, CancellationToken.None);
        await connected.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(FakeOpenSlidesClient.LoginCount).IsEqualTo(1);
        await Assert.That(_handler.Requests.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Error_line_reconnects() {
        _handler.Enqueue(HttpStatusCode.OK, ["""{"error": {"type": "ClientError", "msg": "nope"}}"""], keepOpen: true);
        _handler.Enqueue(HttpStatusCode.OK, [Compact(OpenSlidesFixtures.FullMap)], keepOpen: true);

        await _client.StartAsync(Settings, OpenSlidesFixtures.MeetingId, CancellationToken.None);

        await NextViewAsync(v => v.Polls.Count == 2);
        await Assert.That(_handler.Requests.Count).IsEqualTo(2);
    }
}
