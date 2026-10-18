using System;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using LinqToDB;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quartermaster.Api;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Api.Events;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AssemblyAssistant;
using Quartermaster.Data.Options;
using Quartermaster.Server.AssemblyAssistant.Mumble;
using Quartermaster.Server.AssemblyAssistant.Session;
using Quartermaster.Server.Tests.AssemblyAssistant;
using Quartermaster.Server.Tests.AssemblyAssistant.Fakes;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.AssemblyAssistant;

[NotInParallel(nameof(AssemblySessionHostTests))]
public class AssemblySessionHostTests : IntegrationTestBase {
    private readonly FakeMumbleConnectionFactory _connections = new();
    private readonly RecordingAssemblyPublisher _publisher = new();
    private WebApplicationFactory<Program>? _host;

    [After(Test)]
    public async Task Teardown() {
        if (_host == null)
            return;

        var sessions = _host.Services.GetRequiredService<AssemblySessionHost>();
        if (sessions.IsRunning)
            await sessions.EndSessionAsync(Guid.NewGuid(), CancellationToken.None);
        await _host.DisposeAsync();
    }

    public static void ConfigureMumble(IServiceProvider services) {
        using var scope = services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<OptionRepository>();
        options.SetValue("mumble.host", null, "127.0.0.1");
        options.SetValue("mumble.certificate", null, "ZmFrZQ==");
        foreach (var (role, optionId) in MumbleSettings.ChannelOptionIds)
            options.SetValue(optionId, null, AssemblySessionFixture.Channels[role].ToString());
    }

    private AssemblySessionHost CreateHost(bool configure = true) {
        if (configure)
            ConfigureMumble(Factory.Services);

        _host = Factory.WithWebHostBuilder(b => b.ConfigureServices(services => {
            services.RemoveAll<IMumbleConnectionFactory>();
            services.AddSingleton<IMumbleConnectionFactory>(_connections);
            services.RemoveAll<IAssemblyPublisher>();
            services.AddSingleton<IAssemblyPublisher>(_publisher);
        }));
        return _host.Services.GetRequiredService<AssemblySessionHost>();
    }

    private static MumbleUser User(uint session, string name, uint channel, string? hash = null) {
        return new MumbleUser(session, name, channel, null, hash ?? name, false, false);
    }

    private static MumbleSynced Synced(params MumbleUser[] users) {
        var bot = new MumbleUser(AssemblySessionFixture.BotSession, "Versammlungsassistent", AssemblySessionFixture.Chair, 1, "bot", true, false);
        return new MumbleSynced(new MumbleServerSnapshot(AssemblySessionFixture.BotSession, [], users.Append(bot).ToList()));
    }

    private static async Task<AssemblySnapshotDTO> WaitForAsync(AssemblySessionHost host, Func<AssemblySnapshotDTO, bool> predicate) {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (true) {
            var snapshot = host.Snapshot();
            if (snapshot != null && predicate(snapshot))
                return snapshot;
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("The expected session state was not reached.");
            await Task.Delay(20);
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition) {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition()) {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("The expected condition was not reached.");
            await Task.Delay(20);
        }
    }

    private async Task<(AssemblySessionHost Host, Guid EventId)> StartedSessionAsync() {
        var chapter = Builder.SeedChapter();
        var evt = Builder.SeedEvent(chapter.Id, status: EventStatus.Active);
        var host = CreateHost();
        var result = await host.StartSessionAsync(evt.Id, Guid.NewGuid(), CancellationToken.None);
        await Assert.That(result.Accepted).IsTrue();
        return (host, evt.Id);
    }

    [Test]
    public async Task Start_connects_with_chair_as_home_channel() {
        var (host, eventId) = await StartedSessionAsync();

        await Assert.That(_connections.Connection.Started).IsTrue();
        await Assert.That(_connections.Connection.HomeChannelId).IsEqualTo(AssemblySessionFixture.Chair);
        await Assert.That(host.EventId).IsEqualTo(eventId);
        await Assert.That(Db.AssemblySessions.Single(s => s.EventId == eventId).EndedAt).IsNull();
    }

    [Test]
    public async Task Start_refused_while_other_session_runs() {
        var (host, _) = await StartedSessionAsync();
        var otherEvent = Builder.SeedEvent(Builder.SeedChapter().Id, status: EventStatus.Active);

        var second = await host.StartSessionAsync(otherEvent.Id, Guid.NewGuid(), CancellationToken.None);

        await Assert.That(second.Accepted).IsFalse();
        await Assert.That(second.MessageKey).IsEqualTo(I18nKey.Ui.AssemblyConsole.Reject.SessionRunning);
    }

    [Test]
    public async Task Start_refused_when_not_configured() {
        var evt = Builder.SeedEvent(Builder.SeedChapter().Id, status: EventStatus.Active);
        var host = CreateHost(configure: false);

        var result = await host.StartSessionAsync(evt.Id, Guid.NewGuid(), CancellationToken.None);

        await Assert.That(result.MessageKey).IsEqualTo(I18nKey.Ui.AssemblyConsole.Reject.NotConfigured);
        await Assert.That(host.IsRunning).IsFalse();
    }

    [Test]
    public async Task Command_for_other_event_is_rejected() {
        var (host, _) = await StartedSessionAsync();

        var result = host.Execute(Guid.NewGuid(), (state, now) => state.ReopenList(state.Version, Guid.NewGuid(), now));

        await Assert.That(result.Accepted).IsFalse();
        await Assert.That(result.MessageKey).IsEqualTo(I18nKey.Ui.AssemblyConsole.Reject.NoSession);
    }

    [Test]
    public async Task Effects_are_executed_outside_lock() {
        var (host, _) = await StartedSessionAsync();
        var connection = _connections.Connection;
        connection.Raise(Synced(User(10, "Anna", AssemblySessionFixture.Pro)));
        var snapshot = await WaitForAsync(host, s => s.Queues.Any(q => q.Entries.Count > 0));

        // The move raises the resulting UserState from another thread and waits for it; this would block if the host still held its lock.
        var handledOnOtherThread = false;
        connection.OnMove = (session, channel) => {
            handledOnOtherThread = Task.Run(() => connection.Raise(new MumbleUserEntered(User(session, "Anna", channel), AssemblySessionFixture.Pro)))
                .Wait(TimeSpan.FromSeconds(2));
        };
        var key = snapshot.Queues.Single(q => q.Kind == QueueKind.Pro).Entries.Single().Key;
        var result = host.Execute(host.EventId!.Value, (state, now) => state.GiveFloor(state.Version, key, Guid.NewGuid(), now));

        await Assert.That(result.Accepted).IsTrue();
        await Assert.That(handledOnOtherThread).IsTrue();
        var after = await WaitForAsync(host, s => s.Floor.Any(f => f.Session == 10));
        await Assert.That(after.Floor.Single().Role).IsEqualTo(SpeakerRole.Pro);
    }

    [Test]
    public async Task Move_failure_flows_back_into_state() {
        var (host, _) = await StartedSessionAsync();
        var connection = _connections.Connection;
        connection.MoveResult = (_, _) => new MumbleMoveResult(MumbleMoveOutcome.PermissionDenied, "Move");
        connection.Raise(Synced(User(10, "Anna", AssemblySessionFixture.Pro)));
        var snapshot = await WaitForAsync(host, s => s.Queues.Any(q => q.Entries.Count > 0));
        var key = snapshot.Queues.Single(q => q.Kind == QueueKind.Pro).Entries.Single().Key;

        host.Execute(host.EventId!.Value, (state, now) => state.GiveFloor(state.Version, key, Guid.NewGuid(), now));

        var after = await WaitForAsync(host, s => s.Notices.Any(n => n.MessageKey.StartsWith(I18nKey.Ui.AssemblyConsole.Notice.MoveFailed)));
        await Assert.That(after.Queues.Single(q => q.Kind == QueueKind.Pro).Entries.Single().MovePending).IsFalse();
    }

    [Test]
    public async Task Reconnect_reconciles_queues_and_marks_unknown_uncertain() {
        var (host, _) = await StartedSessionAsync();
        var connection = _connections.Connection;
        connection.Raise(new MumbleConnectionChanged(MumbleConnectionState.Synced, null));
        connection.Raise(Synced(User(10, "Anna", AssemblySessionFixture.Pro)));
        connection.Raise(new MumbleUserEntered(User(11, "Bert", AssemblySessionFixture.Pro), AssemblySessionFixture.Listener));

        connection.Raise(new MumbleConnectionChanged(MumbleConnectionState.Reconnecting, "lost"));
        connection.Raise(Synced(User(21, "Bert", AssemblySessionFixture.Pro), User(22, "Cleo", AssemblySessionFixture.Pro)));
        connection.Raise(new MumbleConnectionChanged(MumbleConnectionState.Synced, null));

        var snapshot = await WaitForAsync(host, s => s.Queues.Single(q => q.Kind == QueueKind.Pro).Entries.Any(e => e.Name == "Cleo"));
        var entries = snapshot.Queues.Single(q => q.Kind == QueueKind.Pro).Entries;
        await Assert.That(entries.Select(e => e.Name)).IsEquivalentTo(["Bert", "Cleo"]);
        await Assert.That(entries.Single(e => e.Name == "Bert").OrderUncertain).IsFalse();
        await Assert.That(entries.Single(e => e.Name == "Bert").Session).IsEqualTo(21u);
        await Assert.That(entries.Single(e => e.Name == "Cleo").OrderUncertain).IsTrue();
        await Assert.That(snapshot.Notices.Any(n => n.MessageKey == I18nKey.Ui.AssemblyConsole.Notice.ConnectionLost)).IsTrue();
        await Assert.That(snapshot.Notices.Any(n => n.MessageKey == I18nKey.Ui.AssemblyConsole.Notice.ConnectionRestored)).IsTrue();
        await Assert.That(snapshot.MumbleState).IsEqualTo(nameof(MumbleConnectionState.Synced));
    }

    [Test]
    public async Task Speech_log_is_persisted_and_end_closes_it() {
        // Filtered by session: the teardown of the previous test may still write its closing entries after this test's table cleanup.
        var (host, eventId) = await StartedSessionAsync();
        var sessionId = host.SessionId!.Value;
        var connection = _connections.Connection;
        connection.Raise(Synced(User(10, "Anna", AssemblySessionFixture.Speaker)));
        await WaitForAsync(host, s => s.Floor.Count == 1);

        connection.Raise(new MumbleSpeakingStarted(10, DateTime.UtcNow));
        await WaitUntilAsync(() => Db.AssemblyLogEntries.Any(e => e.SessionId == sessionId && e.Kind == AssemblyLogKind.Speech));
        var open = Db.AssemblyLogEntries.Single(e => e.SessionId == sessionId && e.Kind == AssemblyLogKind.Speech);
        await Assert.That(open.EndedAt).IsNull();
        await Assert.That(open.MumbleName).IsEqualTo("Anna");

        var ended = await host.EndSessionAsync(Guid.NewGuid(), CancellationToken.None);

        await Assert.That(ended.Accepted).IsTrue();
        await Assert.That(host.IsRunning).IsFalse();
        await Assert.That(_connections.Connection.Stopped).IsTrue();
        await Assert.That(_publisher.EndedCount).IsEqualTo(1);
        var closed = Db.AssemblyLogEntries.Single(e => e.SessionId == sessionId && e.Kind == AssemblyLogKind.Speech);
        await Assert.That(closed.EndedAt).IsNotNull();
        await Assert.That(Db.AssemblySessions.Single(s => s.EventId == eventId).EndedAt).IsNotNull();
        await Assert.That(Db.AssemblyLogEntries.Any(e => e.SessionId == sessionId && e.Kind == AssemblyLogKind.SessionEnded)).IsTrue();
    }

    [Test]
    public async Task Open_session_resumes_on_start_and_timer_resumes_from_log() {
        var evt = Builder.SeedEvent(Builder.SeedChapter().Id, status: EventStatus.Active);
        var startedAt = DateTime.UtcNow.AddSeconds(-100);
        var session = new AssemblySession { EventId = evt.Id, StartedAt = startedAt.AddMinutes(-5), StartedByUserId = Guid.NewGuid() };
        Db.Insert(session);
        Db.Insert(new AssemblyLogEntry {
            SessionId = session.Id,
            At = startedAt,
            Kind = AssemblyLogKind.Speech,
            MumbleName = "Anna",
            Role = nameof(SpeakerRole.Pro),
            StartedAt = startedAt,
            AllottedSeconds = 180
        });

        // The test factory removes hosted services, so the application start is triggered by hand.
        var host = CreateHost();
        await host.StartAsync(CancellationToken.None);
        await Assert.That(host.IsRunning).IsTrue();
        _connections.Connection.Raise(Synced(User(10, "Anna", AssemblySessionFixture.Speaker)));

        var snapshot = await WaitForAsync(host, s => s.Floor.Count == 1);
        await Assert.That(host.SessionId).IsEqualTo(session.Id);
        var timer = snapshot.Floor.Single().Timer;
        await Assert.That(timer.StartedAt!.Value).IsEqualTo(startedAt).Within(TimeSpan.FromMilliseconds(1));
        await Assert.That(timer.AllottedSeconds).IsEqualTo(180);
    }

    [Test]
    public async Task Start_refused_when_event_not_active() {
        var evt = Builder.SeedEvent(Builder.SeedChapter().Id, status: EventStatus.Draft);
        var host = CreateHost();

        var result = await host.StartSessionAsync(evt.Id, Guid.NewGuid(), CancellationToken.None);

        await Assert.That(result.MessageKey).IsEqualTo(I18nKey.Ui.AssemblyConsole.Reject.EventNotActive);
        await Assert.That(host.IsRunning).IsFalse();
        await Assert.That(_connections.Created).IsEqualTo(0);
    }

    [Test]
    public async Task Open_session_of_inactive_event_is_ended_instead_of_resumed() {
        var evt = Builder.SeedEvent(Builder.SeedChapter().Id, status: EventStatus.Completed);
        var session = new AssemblySession { EventId = evt.Id, StartedAt = DateTime.UtcNow.AddDays(-2), StartedByUserId = Guid.NewGuid() };
        Db.Insert(session);

        var host = CreateHost();
        await host.StartAsync(CancellationToken.None);

        await Assert.That(host.IsRunning).IsFalse();
        await Assert.That(_connections.Created).IsEqualTo(0);
        await Assert.That(Db.AssemblySessions.Single(s => s.Id == session.Id).EndedAt).IsNotNull();
        await Assert.That(Db.AssemblyLogEntries.Any(e => e.SessionId == session.Id && e.Kind == AssemblyLogKind.SessionEnded)).IsTrue();
    }

    [Test]
    public async Task Ending_the_event_ends_its_running_session() {
        var (host, eventId) = await StartedSessionAsync();
        var chapterId = Db.Events.Single(e => e.Id == eventId).ChapterId;
        var (user, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapterId] = new[] { PermissionIdentifier.EditEvents } });
        using var client = _host!.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await AttachAntiforgeryTokenAsync(client);

        var response = await client.PutAsJsonAsync($"/api/events/{eventId}/status", new EventStatusUpdateRequest { Id = eventId, Status = EventStatus.Completed });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(host.IsRunning).IsFalse();
        await Assert.That(_connections.Connection.Stopped).IsTrue();
        var session = Db.AssemblySessions.Single(s => s.EventId == eventId);
        await Assert.That(session.EndedAt).IsNotNull();
        var ended = Db.AssemblyLogEntries.Single(e => e.SessionId == session.Id && e.Kind == AssemblyLogKind.SessionEnded);
        await Assert.That(ended.ActorUserId).IsEqualTo(user.Id);
    }

    [Test]
    public async Task Ending_another_event_keeps_the_session_running() {
        var (host, _) = await StartedSessionAsync();
        var other = Builder.SeedEvent(Builder.SeedChapter().Id, status: EventStatus.Active);

        await host.EndSessionOfEventAsync(other.Id, Guid.NewGuid(), CancellationToken.None);

        await Assert.That(host.IsRunning).IsTrue();
        await Assert.That(_connections.Connection.Stopped).IsFalse();
    }

    [Test]
    public async Task Ending_the_event_ends_a_stored_session_that_does_not_run() {
        var evt = Builder.SeedEvent(Builder.SeedChapter().Id, status: EventStatus.Active);
        var session = new AssemblySession { EventId = evt.Id, StartedAt = DateTime.UtcNow, StartedByUserId = Guid.NewGuid() };
        Db.Insert(session);
        var host = CreateHost(configure: false);

        await host.EndSessionOfEventAsync(evt.Id, Guid.NewGuid(), CancellationToken.None);

        await Assert.That(Db.AssemblySessions.Single(s => s.Id == session.Id).EndedAt).IsNotNull();
    }

    [Test]
    public async Task Setup_check_refused_while_session_runs() {
        await StartedSessionAsync();

        using var scope = _host!.Services.CreateScope();
        var check = await scope.ServiceProvider.GetRequiredService<MumbleConnectionCheck>().CheckAsync(CancellationToken.None);

        await Assert.That(check.ErrorKey).IsEqualTo(I18nKey.Ui.AssemblySetup.Error.SessionRunning);
        await Assert.That(_connections.Created).IsEqualTo(1);
    }

    [Test]
    public async Task Repository_open_speech_ignores_unstarted_and_ended() {
        var evt = Builder.SeedEvent(Builder.SeedChapter().Id, status: EventStatus.Active);
        var session = new AssemblySession { EventId = evt.Id, StartedAt = DateTime.UtcNow, StartedByUserId = Guid.NewGuid() };
        Db.Insert(session);
        var now = DateTime.UtcNow;
        Db.Insert(new AssemblyLogEntry { SessionId = session.Id, At = now, Kind = AssemblyLogKind.Speech, MumbleName = "Ended", StartedAt = now.AddMinutes(-3), EndedAt = now });
        Db.Insert(new AssemblyLogEntry { SessionId = session.Id, At = now, Kind = AssemblyLogKind.Speech, MumbleName = "Waiting" });
        var openEntry = new AssemblyLogEntry { SessionId = session.Id, At = now, Kind = AssemblyLogKind.Speech, MumbleName = "Open", StartedAt = now.AddMinutes(-1) };
        Db.Insert(openEntry);

        var repository = new AssemblySessionRepository(Db);
        openEntry.EndedAt = null;
        repository.SaveLog([openEntry]);

        await Assert.That(repository.GetOpenSpeech(session.Id)!.MumbleName).IsEqualTo("Open");
        await Assert.That(repository.GetOpen()!.Id).IsEqualTo(session.Id);
        await Assert.That(repository.GetLog(session.Id).Count).IsEqualTo(3);
    }
}
