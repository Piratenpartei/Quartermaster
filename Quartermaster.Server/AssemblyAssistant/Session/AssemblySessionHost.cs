using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Linq;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.Events;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AssemblyAssistant;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Events;
using Quartermaster.Data.Options;
using Quartermaster.Server.AssemblyAssistant.Mumble;
using Quartermaster.Server.AssemblyAssistant.OpenSlides;
using Quartermaster.Server.AssemblyAssistant.Templates;
using Quartermaster.Server.AssemblyMotions.OpenSlides;

namespace Quartermaster.Server.AssemblyAssistant.Session;

/// <summary>
/// Owns the running assembly session: the state under one lock, the Mumble connection, the 250 ms tick, persistence and
/// publishing. State methods run under the lock; their effects run outside it (moves fire and report failures back,
/// log writes and publishing go through one pump task in order). An open session is resumed when the application starts,
/// as long as its event is still active; a session only runs for an active event.
/// </summary>
public class AssemblySessionHost : IHostedService {
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(250);

    private readonly IServiceScopeFactory _scopes;
    private readonly IMumbleConnectionFactory _connections;
    private readonly IAssemblyPublisher _publisher;
    private readonly IOpenSlidesClient _openSlides;
    private readonly IOpenSlidesAutoupdateClientFactory _autoupdates;
    private readonly TimeProvider _time;
    private readonly ILogger<AssemblySessionHost> _logger;
    private readonly Lock _lock = new();
    private readonly SemaphoreSlim _lifecycle = new(1, 1);

    private AssemblySessionState? state;
    private IMumbleConnection? connection;
    private OpenSlidesAutoupdateClient? autoupdate;
    private OpenSlidesSettings? openSlidesSettings;
    private int? openSlidesMeetingId;
    private Channel<AssemblyEffects>? pending;
    private CancellationTokenSource? runCts;
    private Task? tickTask;
    private Task? pumpTask;
    private bool connectionLost;

    public Guid? SessionId { get; private set; }
    public Guid? EventId { get; private set; }
    public Guid? ChapterId { get; private set; }

    public bool IsRunning {
        get {
            using (_lock.EnterScope()) {
                return state != null;
            }
        }
    }

    public AssemblySessionHost(IServiceScopeFactory scopes, IMumbleConnectionFactory connections, IAssemblyPublisher publisher,
        IOpenSlidesClient openSlides, IOpenSlidesAutoupdateClientFactory autoupdates, TimeProvider time, ILogger<AssemblySessionHost> logger) {
        _scopes = scopes;
        _connections = connections;
        _publisher = publisher;
        _openSlides = openSlides;
        _autoupdates = autoupdates;
        _time = time;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken) {
        try {
            await ResumeOpenSessionAsync(cancellationToken);
        } catch (Exception ex) {
            _logger.LogError(ex, "Resuming the open assembly session failed");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken) {
        await _lifecycle.WaitAsync(cancellationToken);
        try {
            await StopRunningAsync();
        } finally {
            _lifecycle.Release();
        }
    }

    public async Task<AssemblyCommandResult> StartSessionAsync(Guid eventId, Guid userId, CancellationToken ct) {
        await _lifecycle.WaitAsync(ct);
        try {
            if (IsRunning)
                return AssemblyCommandResult.Rejected(I18nKey.Ui.AssemblyConsole.Reject.SessionRunning);

            using var scope = _scopes.CreateScope();
            var services = scope.ServiceProvider;
            var options = services.GetRequiredService<OptionRepository>();
            var settings = MumbleSettings.ReadFrom(options);
            if (!IsUsable(settings))
                return AssemblyCommandResult.Rejected(I18nKey.Ui.AssemblyConsole.Reject.NotConfigured);

            var evt = services.GetRequiredService<EventRepository>().Get(eventId);
            if (evt == null)
                return AssemblyCommandResult.Rejected(I18nKey.Ui.AssemblyConsole.Reject.NoSession);
            if (evt.Status != EventStatus.Active)
                return AssemblyCommandResult.Rejected(I18nKey.Ui.AssemblyConsole.Reject.EventNotActive);

            var now = Now();
            var meetingId = services.GetRequiredService<AssemblyMotionPortalRepository>().GetByEvent(eventId)?.OpenSlidesMeetingId;
            var sessions = services.GetRequiredService<AssemblySessionRepository>();
            var session = sessions.Create(eventId, meetingId, userId, now);
            sessions.SaveLog([SessionLog(session.Id, AssemblyLogKind.SessionStarted, now, userId)]);

            await RunAsync(session, evt.ChapterId, settings, AssemblySessionOptions.ReadFrom(options), null, PollContext.Read(services, session));
            ApplyAndProcess(s => s.OnNotice(I18nKey.Ui.AssemblyConsole.Notice.SessionStarted, now));
            return AssemblyCommandResult.Ok();
        } finally {
            _lifecycle.Release();
        }
    }

    public async Task<AssemblyCommandResult> EndSessionAsync(Guid userId, CancellationToken ct) {
        await _lifecycle.WaitAsync(ct);
        try {
            return await EndRunningAsync(userId);
        } finally {
            _lifecycle.Release();
        }
    }

    /// <summary>Ends the event's open session, whether it runs here or only exists in the database; called when the event stops being active.</summary>
    public async Task EndSessionOfEventAsync(Guid eventId, Guid userId, CancellationToken ct) {
        await _lifecycle.WaitAsync(ct);
        try {
            if (IsRunning && EventId == eventId) {
                await EndRunningAsync(userId);
                return;
            }

            using var scope = _scopes.CreateScope();
            var sessions = scope.ServiceProvider.GetRequiredService<AssemblySessionRepository>();
            var open = sessions.GetOpen();
            if (open == null || open.EventId != eventId)
                return;

            EndStored(sessions, open.Id, userId);
        } finally {
            _lifecycle.Release();
        }
    }

    private async Task<AssemblyCommandResult> EndRunningAsync(Guid? userId) {
        var sessionId = SessionId;
        var eventId = EventId;
        if (!IsRunning || sessionId == null || eventId == null)
            return AssemblyCommandResult.Rejected(I18nKey.Ui.AssemblyConsole.Reject.NoSession);

        var now = Now();
        ApplyAndProcess(s => s.Close(now));
        await StopRunningAsync();

        using (var scope = _scopes.CreateScope()) {
            var sessions = scope.ServiceProvider.GetRequiredService<AssemblySessionRepository>();
            sessions.SaveLog([SessionLog(sessionId.Value, AssemblyLogKind.SessionEnded, now, userId)]);
            sessions.End(sessionId.Value, now);
        }

        await _publisher.PublishEndedAsync(eventId.Value);
        return AssemblyCommandResult.Ok();
    }

    private void EndStored(AssemblySessionRepository sessions, Guid sessionId, Guid? userId) {
        var now = Now();
        sessions.SaveLog([SessionLog(sessionId, AssemblyLogKind.SessionEnded, now, userId)]);
        sessions.End(sessionId, now);
    }

    public AssemblyCommandResult Execute(Guid eventId, Func<AssemblySessionState, DateTime, (AssemblyCommandResult Result, AssemblyEffects Effects)> command) {
        AssemblyCommandResult result;
        AssemblyEffects effects;
        using (_lock.EnterScope()) {
            if (state == null || EventId != eventId)
                return AssemblyCommandResult.Rejected(I18nKey.Ui.AssemblyConsole.Reject.NoSession);

            (result, effects) = command(state, Now());
        }

        Process(effects);
        return result;
    }

    public AssemblySnapshotDTO? Snapshot() {
        using (_lock.EnterScope()) {
            return SnapshotLocked();
        }
    }

    public AssemblyOverlayDTO OverlaySnapshot(Guid eventId) {
        using (_lock.EnterScope()) {
            if (state == null || EventId != eventId)
                return new AssemblyOverlayDTO { ServerNowUtc = Now(), Active = false };

            return state.OverlaySnapshot(Now());
        }
    }

    private async Task ResumeOpenSessionAsync(CancellationToken ct) {
        await _lifecycle.WaitAsync(ct);
        try {
            using var scope = _scopes.CreateScope();
            var services = scope.ServiceProvider;
            var sessions = services.GetRequiredService<AssemblySessionRepository>();
            var open = sessions.GetOpen();
            if (open == null)
                return;

            var options = services.GetRequiredService<OptionRepository>();
            var settings = MumbleSettings.ReadFrom(options);
            var evt = services.GetRequiredService<EventRepository>().Get(open.EventId);
            if (evt != null && evt.Status != EventStatus.Active) {
                _logger.LogInformation("Ending assembly session {SessionId}: its event {EventId} is no longer active", open.Id, open.EventId);
                EndStored(sessions, open.Id, null);
                return;
            }
            if (evt == null || !IsUsable(settings)) {
                _logger.LogWarning("Open assembly session {SessionId} cannot be resumed (event missing or Mumble not configured)", open.Id);
                return;
            }

            _logger.LogInformation("Resuming assembly session {SessionId} of event {EventId}", open.Id, open.EventId);
            await RunAsync(open, evt.ChapterId, settings, AssemblySessionOptions.ReadFrom(options), sessions.GetOpenSpeech(open.Id), PollContext.Read(services, open));
        } finally {
            _lifecycle.Release();
        }
    }

    private async Task RunAsync(AssemblySession session, Guid chapterId, MumbleSettings settings, AssemblySessionOptions options, AssemblyLogEntry? openSpeech,
        PollContext polls) {
        var pollState = new AssemblyPollState(polls.Config);
        var newState = new AssemblySessionState(session.Id, settings.Channels, options, pollState) { MumbleState = MumbleConnectionState.Connecting };
        newState.PrepareRecovery(openSpeech);

        var cts = new CancellationTokenSource();
        var effectsChannel = Channel.CreateUnbounded<AssemblyEffects>(new UnboundedChannelOptions { SingleReader = true });
        var newConnection = _connections.Create();
        using (_lock.EnterScope()) {
            state = newState;
            SessionId = session.Id;
            EventId = session.EventId;
            ChapterId = chapterId;
            connection = newConnection;
            pending = effectsChannel;
            runCts = cts;
            connectionLost = false;
            openSlidesSettings = polls.OpenSlides;
            openSlidesMeetingId = session.OpenSlidesMeetingId;
        }

        pumpTask = Task.Run(() => PumpAsync(effectsChannel.Reader));
        newConnection.EventRaised += OnMumbleEvent;
        await newConnection.StartAsync(settings, settings.Channels[ChannelRole.Chair], cts.Token);
        tickTask = Task.Run(() => TickLoopAsync(cts.Token));
        if (polls.Config.Enabled)
            await StartAutoupdateAsync(polls.OpenSlides!, session.OpenSlidesMeetingId!.Value, cts.Token);
        effectsChannel.Writer.TryWrite(AssemblyEffects.None with { Changed = true });
    }

    private async Task StartAutoupdateAsync(OpenSlidesSettings settings, int meetingId, CancellationToken ct) {
        var client = _autoupdates.Create();
        client.Changed += view => ApplyAndProcess(s => s.OnOpenSlidesView(view, Now()));
        client.ConnectionChanged += (connected, error) => ApplyAndProcess(s => s.OnOpenSlidesConnection(connected, error, Now()));
        using (_lock.EnterScope()) {
            autoupdate = client;
        }
        await client.StartAsync(settings, meetingId, ct);
    }

    private async Task StopRunningAsync() {
        IMumbleConnection? oldConnection;
        OpenSlidesAutoupdateClient? oldAutoupdate;
        CancellationTokenSource? cts;
        Channel<AssemblyEffects>? oldPending;
        using (_lock.EnterScope()) {
            oldConnection = connection;
            oldAutoupdate = autoupdate;
            cts = runCts;
            oldPending = pending;
        }
        if (cts == null)
            return;

        await cts.CancelAsync();
        await IgnoreCancellation(tickTask);
        if (oldAutoupdate != null)
            await oldAutoupdate.DisposeAsync();
        if (oldConnection != null) {
            oldConnection.EventRaised -= OnMumbleEvent;
            await oldConnection.StopAsync();
            await oldConnection.DisposeAsync();
        }

        oldPending?.Writer.TryComplete();
        await IgnoreCancellation(pumpTask);
        cts.Dispose();

        using (_lock.EnterScope()) {
            state = null;
            connection = null;
            autoupdate = null;
            openSlidesSettings = null;
            openSlidesMeetingId = null;
            pending = null;
            runCts = null;
            SessionId = null;
            EventId = null;
            ChapterId = null;
        }
        tickTask = null;
        pumpTask = null;
    }

    private void OnMumbleEvent(MumbleEvent mumbleEvent) {
        ApplyAndProcess(s => Apply(s, mumbleEvent, Now()));
    }

    private AssemblyEffects Apply(AssemblySessionState current, MumbleEvent mumbleEvent, DateTime now) {
        switch (mumbleEvent) {
            case MumbleSynced synced:
                var effects = current.OnSynced(synced.Snapshot, now);
                if (!connectionLost)
                    return effects;

                connectionLost = false;
                current.OnNotice(I18nKey.Ui.AssemblyConsole.Notice.ConnectionRestored, now);
                return effects;
            case MumbleUserEntered entered:
                return current.OnUserEntered(entered.User, entered.PreviousChannelId, now);
            case MumbleUserDisconnected disconnected:
                return current.OnUserDisconnected(disconnected.User, now);
            case MumbleSpeakingStarted started:
                return current.OnSpeaking(started.Session, true, now);
            case MumbleSpeakingStopped stopped:
                return current.OnSpeaking(stopped.Session, false, now);
            case MumbleSelfMovedBack:
                return current.OnNotice(I18nKey.Ui.AssemblyConsole.Notice.BotMovedBack, now);
            case MumbleConnectionChanged changed:
                return ApplyConnectionChange(current, changed, now);
            default:
                return AssemblyEffects.None;
        }
    }

    private AssemblyEffects ApplyConnectionChange(AssemblySessionState current, MumbleConnectionChanged changed, DateTime now) {
        var wasSynced = current.MumbleState == MumbleConnectionState.Synced;
        current.MumbleState = changed.State;
        current.MumbleError = changed.Error;
        if (wasSynced && changed.State != MumbleConnectionState.Synced) {
            connectionLost = true;
            return current.OnNotice(I18nKey.Ui.AssemblyConsole.Notice.ConnectionLost, now);
        }

        return AssemblyEffects.None with { Changed = true };
    }

    private void ApplyAndProcess(Func<AssemblySessionState, AssemblyEffects> apply) {
        AssemblyEffects effects;
        using (_lock.EnterScope()) {
            if (state == null)
                return;

            effects = apply(state);
        }

        Process(effects);
    }

    private void Process(AssemblyEffects effects) {
        IMumbleConnection? current;
        Channel<AssemblyEffects>? queue;
        CancellationToken token;
        using (_lock.EnterScope()) {
            current = connection;
            queue = pending;
            token = runCts?.Token ?? CancellationToken.None;
        }

        if (current != null) {
            foreach (var move in effects.Moves)
                _ = MoveAsync(current, move, token);
            foreach (var text in effects.Texts)
                current.QueueText(text.Session, text.Text);
        }

        foreach (var action in effects.OpenSlides)
            _ = OpenSlidesActionAsync(action, token);

        if (effects.Changed || effects.Log.Count > 0)
            queue?.Writer.TryWrite(effects);
    }

    /// <summary>
    /// Creates a template poll on the current agenda item: the procedural topic first if it is missing, then the poll, then
    /// starts it when auto start is on. The steps run outside the lock; OpenSlides refusals come back as the rejection.
    /// </summary>
    public async Task<AssemblyCommandResult> CreatePollAsync(Guid eventId, long version, PollTemplateRequest request, Guid actor, CancellationToken ct) {
        PollPlan? plan;
        string? errorKey;
        bool autoStart;
        OpenSlidesSettings? settings;
        int? meetingId;
        using (_lock.EnterScope()) {
            if (state == null || EventId != eventId)
                return AssemblyCommandResult.Rejected(I18nKey.Ui.AssemblyConsole.Reject.NoSession);
            if (state.Version != version)
                return AssemblyCommandResult.Rejected(I18nKey.Ui.AssemblyConsole.Reject.Stale);

            settings = openSlidesSettings;
            meetingId = openSlidesMeetingId;
            if (settings == null || meetingId == null)
                return AssemblyCommandResult.Rejected(I18nKey.Ui.AssemblyConsole.OpenSlidesDisabled);

            (plan, errorKey) = AssemblyPollTemplates.Plan(request, state.GetPollPlanContext(meetingId.Value));
            autoStart = state.AutoStartPolls;
        }
        if (plan == null)
            return AssemblyCommandResult.Rejected(errorKey!);

        try {
            var contentObject = plan.ContentObjectId
                ?? $"topic/{await _openSlides.CreateTopicAsync(settings, meetingId.Value, plan.NewTopicTitle!, plan.NewTopicParentId!.Value, ct)}";
            var pollId = await _openSlides.CreatePollAsync(settings, plan.Build(contentObject), ct);
            ApplyAndProcess(s => s.RegisterCreatedPoll(pollId, plan, actor, Now()));
            if (autoStart)
                await _openSlides.StartPollAsync(settings, pollId, ct);

            return AssemblyCommandResult.Ok();
        } catch (OpenSlidesException ex) {
            _logger.LogWarning(ex, "Creating the template poll {Template} failed", request.Template);
            return AssemblyCommandResult.Rejected(I18nParams.With(I18nKey.Ui.AssemblyConsole.Reject.OpenSlidesFailed, ("reason", ex.Message)));
        }
    }

    /// <summary>Creates the procedural topic "&lt;number&gt;: Verfahren" for every motion of the agenda that has none yet.</summary>
    public async Task<AssemblyCommandResult> CreateProceduralTopicsAsync(Guid eventId, Guid actor, CancellationToken ct) {
        List<OpenSlidesAgendaItemView> missing;
        OpenSlidesSettings? settings;
        int? meetingId;
        using (_lock.EnterScope()) {
            if (state == null || EventId != eventId)
                return AssemblyCommandResult.Rejected(I18nKey.Ui.AssemblyConsole.Reject.NoSession);

            settings = openSlidesSettings;
            meetingId = openSlidesMeetingId;
            if (settings == null || meetingId == null)
                return AssemblyCommandResult.Rejected(I18nKey.Ui.AssemblyConsole.OpenSlidesDisabled);

            missing = ProceduralTopics.MotionsWithout(state.OpenSlidesView);
        }

        var created = 0;
        try {
            foreach (var item in missing) {
                await _openSlides.CreateTopicAsync(settings, meetingId.Value, ProceduralTopics.TitleFor(item), item.Id, ct);
                created++;
            }
        } catch (OpenSlidesException ex) {
            _logger.LogWarning(ex, "Creating procedural topics failed after {Count}", created);
            ApplyAndProcess(s => s.OnProceduralTopicsCreated(created, actor, Now()));
            return AssemblyCommandResult.Rejected(I18nParams.With(I18nKey.Ui.AssemblyConsole.Reject.OpenSlidesFailed, ("reason", ex.Message)));
        }

        ApplyAndProcess(s => s.OnProceduralTopicsCreated(created, actor, Now()));
        return AssemblyCommandResult.Ok();
    }

    // A started poll goes onto the projectors, followed by the voting countdown next to it.
    private async Task ProjectAsync(OpenSlidesSettings settings, ProjectPollEffect project, CancellationToken ct) {
        int? meetingId;
        int? countdownId;
        using (_lock.EnterScope()) {
            meetingId = openSlidesMeetingId;
            countdownId = state?.OpenSlidesView.PollCountdown?.Id;
        }
        if (meetingId == null)
            return;

        await _openSlides.ProjectAsync(settings, meetingId.Value, project.ProjectorIds, $"poll/{project.PollId}", "poll", ct);
        if (countdownId != null)
            await _openSlides.ProjectAsync(settings, meetingId.Value, project.ProjectorIds, $"projector_countdown/{countdownId}", null, ct);
    }

    private async Task OpenSlidesActionAsync(OpenSlidesEffect action, CancellationToken ct) {
        OpenSlidesSettings? settings;
        using (_lock.EnterScope()) {
            settings = openSlidesSettings;
        }
        if (settings == null)
            return;

        try {
            switch (action) {
                case StopPollEffect stop:
                    await _openSlides.StopPollAsync(settings, stop.PollId, ct);
                    break;
                case PublishPollEffect publish:
                    await _openSlides.PublishPollAsync(settings, publish.PollId, ct);
                    break;
                case SetCountdownEffect countdown:
                    await _openSlides.UpdateCountdownAsync(settings, countdown.CountdownId, countdown.Running, countdown.CountdownTime, ct);
                    break;
                case ResetPollEffect reset:
                    await _openSlides.ResetPollAsync(settings, reset.PollId, ct);
                    break;
                case DeletePollEffect delete:
                    await _openSlides.DeletePollAsync(settings, delete.PollId, ct);
                    break;
                case ProjectPollEffect project:
                    await ProjectAsync(settings, project, ct);
                    break;
            }
        } catch (OperationCanceledException) when (ct.IsCancellationRequested) {
        } catch (Exception ex) {
            _logger.LogError(ex, "OpenSlides action {Action} failed", action);
            var reason = ex is OpenSlidesException openSlidesException ? openSlidesException.Message : ex.GetType().Name;
            ApplyAndProcess(s => s.OnNotice(I18nParams.With(I18nKey.Ui.AssemblyConsole.Notice.OpenSlidesActionFailed, ("reason", reason)), Now()));
        }
    }

    private async Task MoveAsync(IMumbleConnection current, AssemblyMove move, CancellationToken ct) {
        try {
            var result = await current.MoveAsync(move.Session, move.ChannelId, ct);
            if (result.Outcome != MumbleMoveOutcome.Moved)
                ApplyAndProcess(s => s.OnMoveFailed(move.Session, result, Now()));
        } catch (OperationCanceledException) when (ct.IsCancellationRequested) {
        } catch (Exception ex) {
            _logger.LogError(ex, "Moving Mumble session {Session} to channel {Channel} failed", move.Session, move.ChannelId);
            ApplyAndProcess(s => s.OnMoveFailed(move.Session, new MumbleMoveResult(MumbleMoveOutcome.NotConnected, ex.Message), Now()));
        }
    }

    private async Task TickLoopAsync(CancellationToken ct) {
        using var timer = new PeriodicTimer(TickInterval, _time);
        while (await timer.WaitForNextTickAsync(ct))
            ApplyAndProcess(s => s.Tick(Now()));
    }

    // One reader writes logs in order and coalesces bursts of changes into a single publish.
    private async Task PumpAsync(ChannelReader<AssemblyEffects> reader) {
        while (await reader.WaitToReadAsync()) {
            var logs = new List<AssemblyLogEntry>();
            var changed = false;
            while (reader.TryRead(out var effects)) {
                logs.AddRange(effects.Log);
                changed |= effects.Changed;
            }

            SaveLogs(logs);
            if (changed)
                await PublishAsync();
        }
    }

    private void SaveLogs(List<AssemblyLogEntry> logs) {
        if (logs.Count == 0)
            return;

        try {
            using var scope = _scopes.CreateScope();
            scope.ServiceProvider.GetRequiredService<AssemblySessionRepository>().SaveLog(logs);
        } catch (Exception ex) {
            _logger.LogError(ex, "Saving {Count} assembly log entries failed", logs.Count);
        }
    }

    private async Task PublishAsync() {
        AssemblySnapshotDTO? snapshot;
        AssemblyOverlayDTO? overlay;
        Guid? eventId;
        using (_lock.EnterScope()) {
            snapshot = SnapshotLocked();
            overlay = state?.OverlaySnapshot(Now());
            eventId = EventId;
        }
        if (snapshot == null || overlay == null || eventId == null)
            return;

        try {
            await _publisher.PublishAsync(eventId.Value, snapshot, overlay);
        } catch (Exception ex) {
            _logger.LogError(ex, "Publishing the assembly snapshot failed");
        }
    }

    private AssemblySnapshotDTO? SnapshotLocked() {
        if (state == null || EventId == null)
            return null;

        var snapshot = state.Snapshot(Now());
        snapshot.EventId = EventId.Value;
        return snapshot;
    }

    private DateTime Now() {
        return _time.GetUtcNow().UtcDateTime;
    }

    private static bool IsUsable(MumbleSettings settings) {
        return settings.IsConfigured() && settings.Channels.ContainsKey(ChannelRole.Chair);
    }

    private static AssemblyLogEntry SessionLog(Guid sessionId, AssemblyLogKind kind, DateTime now, Guid? userId) {
        return new AssemblyLogEntry { SessionId = sessionId, At = now, Kind = kind, ActorUserId = userId };
    }

    /// <summary>What a session needs from OpenSlides: the access and the poll configuration derived from options and the motion portal.</summary>
    private record PollContext(OpenSlidesSettings? OpenSlides, AssemblyPollConfig Config) {
        public static PollContext Read(IServiceProvider services, AssemblySession session) {
            var options = services.GetRequiredService<OptionRepository>();
            var openSlides = OpenSlidesSettings.ReadFrom(options);
            var settings = PollSettings.ReadFrom(options);
            if (session.OpenSlidesMeetingId == null || !openSlides.IsConfigured())
                return new PollContext(null, AssemblyPollConfig.Disabled(settings));

            var majorities = new Dictionary<int, RequiredMajority>();
            var competitors = new Dictionary<int, IReadOnlyList<CompetingMotion>>();
            var portals = services.GetRequiredService<AssemblyMotionPortalRepository>();
            var portal = portals.GetByEvent(session.EventId);
            if (portal != null)
                ReadMotions(services, portals, portal.Id, majorities, competitors);

            var delegates = int.TryParse(options.GetGlobalValue("assembly.openslides.delegates_group_id")?.Value, out var group) ? group : (int?)null;
            var projectors = (options.GetGlobalValue("assembly.openslides.projector_ids")?.Value ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(v => int.TryParse(v, out var id) ? id : (int?)null)
                .Where(id => id != null)
                .Select(id => id!.Value)
                .ToList();

            return new PollContext(openSlides, new AssemblyPollConfig(true, settings, majorities, delegates, projectors, competitors));
        }

        // Majority per motion type, and the competing motions of each motion with their numbers, keyed by OpenSlides motion id.
        private static void ReadMotions(IServiceProvider services, AssemblyMotionPortalRepository portals, Guid portalId,
            Dictionary<int, RequiredMajority> majorities, Dictionary<int, IReadOnlyList<CompetingMotion>> competitors) {
            var types = portals.GetTypes(portalId).ToDictionary(t => t.Id);
            var motions = services.GetRequiredService<AssemblyMotionRepository>().ListNumberedByPortal(portalId)
                .Where(m => m.OpenSlidesMotionId != null)
                .ToDictionary(m => m.Id);
            foreach (var motion in motions.Values)
                majorities[motion.OpenSlidesMotionId!.Value] = types.TryGetValue(motion.TypeId, out var type) ? type.RequiredMajority : RequiredMajority.Simple;

            var competitions = services.GetRequiredService<AssemblyMotionCompetitionRepository>().ListForMotions(motions.Keys.ToList());
            foreach (var (motionId, links) in competitions) {
                if (!motions.TryGetValue(motionId, out var motion))
                    continue;

                competitors[motion.OpenSlidesMotionId!.Value] = links
                    .Select(link => link.MotionAId == motionId ? link.MotionBId : link.MotionAId)
                    .Where(other => motions.ContainsKey(other))
                    .Select(other => motions[other])
                    .Select(other => new CompetingMotion(other.OpenSlidesMotionId!.Value, Label(other, types)))
                    .ToList();
            }
        }

        private static string Label(AssemblyMotion motion, IReadOnlyDictionary<Guid, AssemblyMotionType> types) {
            var number = types.TryGetValue(motion.TypeId, out var type) && motion.Number != null
                ? AssemblyMotionNumber.Format(type.Code, motion.Number.Value)
                : "";
            return $"{number} {motion.Title}".Trim();
        }
    }

    private static async Task IgnoreCancellation(Task? task) {
        if (task == null)
            return;

        try {
            await task;
        } catch (OperationCanceledException) {
        }
    }
}
