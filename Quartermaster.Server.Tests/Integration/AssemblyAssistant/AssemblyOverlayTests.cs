using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quartermaster.Api;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Api.Events;
using Quartermaster.Server.AssemblyAssistant.Mumble;
using Quartermaster.Server.AssemblyAssistant.Session;
using Quartermaster.Server.Tests.AssemblyAssistant;
using Quartermaster.Server.Tests.AssemblyAssistant.Fakes;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.AssemblyAssistant;

[NotInParallel(nameof(AssemblySessionHostTests))]
public class AssemblyOverlayTests : IntegrationTestBase {
    private static readonly string[] ForbiddenPropertyNames = ["Session", "Key", "Id", "UserId", "EventId", "SessionId"];

    private readonly FakeMumbleConnectionFactory _connections = new();
    private WebApplicationFactory<Program> _host = default!;
    private Guid _eventId;
    private string _chairToken = "";

    [Before(Test)]
    public void Setup() {
        AssemblySessionHostTests.ConfigureMumble(Factory.Services);
        _host = Factory.WithWebHostBuilder(b => b.ConfigureServices(services => {
            services.RemoveAll<IMumbleConnectionFactory>();
            services.AddSingleton<IMumbleConnectionFactory>(_connections);
        }));

        var chapter = Builder.SeedChapter();
        _eventId = Builder.SeedEvent(chapter.Id, status: EventStatus.Active).Id;
        (_, _chairToken) = Builder.SeedAuthenticatedUser(chapterPermissions: new() { [chapter.Id] = [PermissionIdentifier.AssemblyChair] });
    }

    [After(Test)]
    public async Task Teardown() {
        var sessions = _host.Services.GetRequiredService<AssemblySessionHost>();
        if (sessions.IsRunning)
            await sessions.EndSessionAsync(Guid.NewGuid(), CancellationToken.None);
        await _host.DisposeAsync();
    }

    private HubConnection AnonymousConnection() {
        var handler = _host.Server.CreateHandler();
        return new HubConnectionBuilder()
            .WithUrl(new Uri(_host.Server.BaseAddress, AssemblyHubMethods.HubPath), options => {
                options.HttpMessageHandlerFactory = _ => handler;
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();
    }

    private async Task StartSessionAsync(params MumbleUser[] users) {
        var client = _host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _chairToken);
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsync($"/api/assembly/{_eventId}/session/start", JsonContent.Create(new { }));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var bot = new MumbleUser(AssemblySessionFixture.BotSession, "Versammlungsassistent", AssemblySessionFixture.Chair, 1, "bot", true, false);
        _connections.Connection.Raise(new MumbleSynced(new MumbleServerSnapshot(AssemblySessionFixture.BotSession, [], users.Append(bot).ToList())));
    }

    private static MumbleUser User(uint session, string name, uint channel) {
        return new MumbleUser(session, name, channel, null, name, false, false);
    }

    [Test]
    [Arguments(typeof(AssemblyOverlayDTO))]
    [Arguments(typeof(AssemblyTimerDTO))]
    public async Task Overlay_dto_has_no_identifiers(Type type) {
        var names = type.GetProperties().Select(p => p.Name).ToList();

        await Assert.That(names.Intersect(ForbiddenPropertyNames)).IsEmpty();
    }

    [Test]
    public async Task Overlay_updates_on_speaker_change() {
        await using var overlay = AnonymousConnection();
        var speaker = new TaskCompletionSource<AssemblyOverlayDTO>(TaskCreationOptions.RunContinuationsAsynchronously);
        overlay.On<AssemblyOverlayDTO>(AssemblyHubMethods.OverlayChanged, dto => {
            if (dto.SpeakerName == "Anna")
                speaker.TrySetResult(dto);
        });
        await overlay.StartAsync();
        await overlay.InvokeAsync<AssemblyOverlayDTO>("JoinOverlay", _eventId);

        await StartSessionAsync(User(10, "Anna", AssemblySessionFixture.Pro));
        _connections.Connection.Raise(new MumbleUserEntered(User(10, "Anna", AssemblySessionFixture.Speaker), AssemblySessionFixture.Pro));

        var pushed = await speaker.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(pushed.Active).IsTrue();
        await Assert.That(pushed.SpeakerRole).IsEqualTo(SpeakerRole.Pro);
        await Assert.That(pushed.Timer!.AllottedSeconds).IsEqualTo(180);
    }

    [Test]
    public async Task Overlay_of_other_event_is_inactive() {
        await StartSessionAsync(User(10, "Anna", AssemblySessionFixture.Speaker));
        await using var overlay = AnonymousConnection();
        await overlay.StartAsync();

        var other = await overlay.InvokeAsync<AssemblyOverlayDTO>("JoinOverlay", Guid.NewGuid());
        var own = await overlay.InvokeAsync<AssemblyOverlayDTO>("JoinOverlay", _eventId);

        await Assert.That(other.Active).IsFalse();
        await Assert.That(other.SpeakerName).IsNull();
        await Assert.That(own.Active).IsTrue();
    }
}
