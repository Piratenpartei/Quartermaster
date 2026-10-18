using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quartermaster.Api;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Api.Events;
using Quartermaster.Api.I18n;
using Quartermaster.Server.AssemblyAssistant.Mumble;
using Quartermaster.Server.AssemblyAssistant.Session;
using Quartermaster.Server.Tests.AssemblyAssistant;
using Quartermaster.Server.Tests.AssemblyAssistant.Fakes;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.AssemblyAssistant;

[NotInParallel(nameof(AssemblySessionHostTests))]
public class AssemblyHubTests : IntegrationTestBase {
    private readonly FakeMumbleConnectionFactory _connections = new();
    private WebApplicationFactory<Program> _host = default!;
    private Guid _chapterId;
    private Guid _eventId;
    private string _chairToken = "";
    private string _viewToken = "";
    private string _plainToken = "";

    [Before(Test)]
    public void Setup() {
        AssemblySessionHostTests.ConfigureMumble(Factory.Services);
        _host = Factory.WithWebHostBuilder(b => b.ConfigureServices(services => {
            services.RemoveAll<IMumbleConnectionFactory>();
            services.AddSingleton<IMumbleConnectionFactory>(_connections);
        }));

        var chapter = Builder.SeedChapter();
        _chapterId = chapter.Id;
        _eventId = Builder.SeedEvent(chapter.Id, status: EventStatus.Active).Id;
        (_, _chairToken) = Builder.SeedAuthenticatedUser(chapterPermissions: new() {
            [chapter.Id] = [PermissionIdentifier.AssemblyChair, PermissionIdentifier.AssemblyView]
        });
        (_, _viewToken) = Builder.SeedAuthenticatedUser(chapterPermissions: new() { [chapter.Id] = [PermissionIdentifier.AssemblyView] });
        (_, _plainToken) = Builder.SeedAuthenticatedUser();
    }

    [After(Test)]
    public async Task Teardown() {
        var sessions = _host.Services.GetRequiredService<AssemblySessionHost>();
        if (sessions.IsRunning)
            await sessions.EndSessionAsync(Guid.NewGuid(), CancellationToken.None);
        await _host.DisposeAsync();
    }

    private HubConnection Connection(string? token) {
        var handler = _host.Server.CreateHandler();
        return new HubConnectionBuilder()
            .WithUrl(new Uri(_host.Server.BaseAddress, AssemblyHubMethods.HubPath), options => {
                options.HttpMessageHandlerFactory = _ => handler;
                options.Transports = HttpTransportType.LongPolling;
                if (token != null)
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();
    }

    private async Task<HttpClient> HttpAsync(string token) {
        var client = _host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await AttachAntiforgeryTokenAsync(client);
        return client;
    }

    private async Task StartSessionWithAsync(params MumbleUser[] users) {
        var client = await HttpAsync(_chairToken);
        var response = await client.PostAsync($"/api/assembly/{_eventId}/session/start", JsonContent.Create(new { }));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var bot = new MumbleUser(AssemblySessionFixture.BotSession, "Versammlungsassistent", AssemblySessionFixture.Chair, 1, "bot", true, false);
        _connections.Connection.Raise(new MumbleSynced(new MumbleServerSnapshot(AssemblySessionFixture.BotSession, [], users.Append(bot).ToList())));
    }

    private static MumbleUser User(uint session, string name, uint channel) {
        return new MumbleUser(session, name, channel, null, name, false, false);
    }

    [Test]
    public async Task Console_join_requires_view_permission() {
        await using var plain = Connection(_plainToken);
        await using var viewer = Connection(_viewToken);
        await using var chair = Connection(_chairToken);
        await plain.StartAsync();
        await viewer.StartAsync();
        await chair.StartAsync();

        var plainJoin = await plain.InvokeAsync<AssemblyConsoleJoinDTO>("JoinConsole", _eventId);
        var viewerJoin = await viewer.InvokeAsync<AssemblyConsoleJoinDTO>("JoinConsole", _eventId);
        var chairJoin = await chair.InvokeAsync<AssemblyConsoleJoinDTO>("JoinConsole", _eventId);

        await Assert.That(plainJoin.Allowed).IsFalse();
        await Assert.That(viewerJoin.Allowed).IsTrue();
        await Assert.That(viewerJoin.CanControl).IsFalse();
        await Assert.That(chairJoin.CanControl).IsTrue();
        await Assert.That(chairJoin.Snapshot).IsNull();
    }

    [Test]
    public async Task Overlay_join_is_anonymous_and_gets_overlay_dto_only() {
        await using var overlay = Connection(null);
        var updates = new List<AssemblyOverlayDTO>();
        var updatesLock = new Lock();
        overlay.On<AssemblyOverlayDTO>(AssemblyHubMethods.OverlayChanged, dto => {
            using (updatesLock.EnterScope()) {
                updates.Add(dto);
            }
        });
        await overlay.StartAsync();

        var before = await overlay.InvokeAsync<AssemblyOverlayDTO>("JoinOverlay", _eventId);
        await StartSessionWithAsync(User(10, "Anna", AssemblySessionFixture.Speaker));

        await Assert.That(before.Active).IsFalse();
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline) {
            using (updatesLock.EnterScope()) {
                if (updates.Any(u => u.SpeakerName == "Anna"))
                    break;
            }
            await Task.Delay(20);
        }

        AssemblyOverlayDTO last;
        using (updatesLock.EnterScope()) {
            last = updates.Last(u => u.SpeakerName != null);
        }
        await Assert.That(last.Active).IsTrue();
        await Assert.That(last.SpeakerRole).IsEqualTo(SpeakerRole.Manual);
    }

    [Test]
    public async Task Command_requires_chair_permission() {
        await StartSessionWithAsync(User(10, "Anna", AssemblySessionFixture.Pro));
        await using var viewer = Connection(_viewToken);
        await viewer.StartAsync();

        await Assert.That(async () => await viewer.InvokeAsync<AssemblyCommandResult>("ReopenList", _eventId, 0L))
            .Throws<HubException>();
        await Assert.That(_connections.Connection.Moves).IsEmpty();
    }

    [Test]
    public async Task Stale_version_command_is_rejected() {
        await StartSessionWithAsync(User(10, "Anna", AssemblySessionFixture.Pro), User(11, "Bert", AssemblySessionFixture.Contra));
        await using var chairA = Connection(_chairToken);
        await using var chairB = Connection(_chairToken);
        await chairA.StartAsync();
        await chairB.StartAsync();
        var join = await chairA.InvokeAsync<AssemblyConsoleJoinDTO>("JoinConsole", _eventId);
        var snapshot = join.Snapshot!;
        var anna = snapshot.Queues.Single(q => q.Kind == QueueKind.Pro).Entries.Single().Key;
        var bert = snapshot.Queues.Single(q => q.Kind == QueueKind.Contra).Entries.Single().Key;

        var first = await chairA.InvokeAsync<AssemblyCommandResult>("GiveFloor", _eventId, snapshot.Version, anna);
        var second = await chairB.InvokeAsync<AssemblyCommandResult>("GiveFloor", _eventId, snapshot.Version, bert);

        await Assert.That(first.Accepted).IsTrue();
        await Assert.That(second.Accepted).IsFalse();
        await Assert.That(second.MessageKey).IsEqualTo(I18nKey.Ui.AssemblyConsole.Reject.Stale);
        await Assert.That(_connections.Connection.Moves.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Snapshot_is_pushed_after_command() {
        await StartSessionWithAsync(User(10, "Anna", AssemblySessionFixture.Pro));
        await using var chair = Connection(_chairToken);
        var closed = new TaskCompletionSource<AssemblySnapshotDTO>(TaskCreationOptions.RunContinuationsAsynchronously);
        chair.On<AssemblySnapshotDTO>(AssemblyHubMethods.SnapshotChanged, dto => {
            if (dto.ListClosedAt != null)
                closed.TrySetResult(dto);
        });
        await chair.StartAsync();
        var join = await chair.InvokeAsync<AssemblyConsoleJoinDTO>("JoinConsole", _eventId);

        var result = await chair.InvokeAsync<AssemblyCommandResult>("CloseList", _eventId, join.Snapshot!.Version, (DateTime?)null);

        await Assert.That(result.Accepted).IsTrue();
        var pushed = await closed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(pushed.EventId).IsEqualTo(_eventId);
    }

    [Test]
    public async Task Session_endpoints_require_chair_permission() {
        var viewer = await HttpAsync(_viewToken);
        var chair = await HttpAsync(_chairToken);

        var forbidden = await viewer.PostAsync($"/api/assembly/{_eventId}/session/start", JsonContent.Create(new { }));
        var started = await chair.PostAsync($"/api/assembly/{_eventId}/session/start", JsonContent.Create(new { }));
        var again = await chair.PostAsync($"/api/assembly/{_eventId}/session/start", JsonContent.Create(new { }));
        var unknown = await chair.PostAsync($"/api/assembly/{Guid.NewGuid()}/session/start", JsonContent.Create(new { }));
        var ended = await chair.PostAsync($"/api/assembly/{_eventId}/session/end", JsonContent.Create(new { }));

        await Assert.That(forbidden.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(started.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(again.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        var againResult = await again.Content.ReadFromJsonAsync<AssemblyCommandResult>();
        await Assert.That(againResult!.MessageKey).IsEqualTo(I18nKey.Ui.AssemblyConsole.Reject.SessionRunning);
        await Assert.That(unknown.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(ended.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}
