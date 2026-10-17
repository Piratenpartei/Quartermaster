using System;
using System.Collections.Generic;
using System.Linq;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AssemblyAssistant;
using Quartermaster.Server.AssemblyAssistant.Flow;
using Quartermaster.Server.AssemblyAssistant.Mumble;
using Quartermaster.Server.AssemblyAssistant.OpenSlides;
using Quartermaster.Server.AssemblyAssistant.Templates;

namespace Quartermaster.Server.AssemblyAssistant.Session;

/// <summary>
/// The rules of a running assembly session: request queues, floor, speaking timers, list of speakers. Pure and
/// single-threaded: every method takes the current time and returns the side effects instead of performing them; the
/// host serialises calls under its lock.
///
/// <see cref="Version"/> counts what a second chair must have seen before deciding: chair commands, changes of the floor,
/// reconnects and failed moves. Members entering or leaving request channels, speaking indicators and timer flags are
/// published without bumping it, so a busy assembly does not turn every click into a stale command, while two chairs
/// giving the floor at the same moment still cannot both succeed.
/// </summary>
public class AssemblySessionState {
    private const int MaxNotices = 30;

    private static readonly IReadOnlyDictionary<ChannelRole, QueueKind> QueueKindByRole = new Dictionary<ChannelRole, QueueKind> {
        [ChannelRole.Point] = QueueKind.Point,
        [ChannelRole.Question] = QueueKind.Question,
        [ChannelRole.Pro] = QueueKind.Pro,
        [ChannelRole.Contra] = QueueKind.Contra
    };

    private readonly Guid _sessionId;
    private readonly IReadOnlyDictionary<ChannelRole, uint> _channels;
    private readonly Dictionary<uint, ChannelRole> _roleByChannel;
    private readonly AssemblySessionOptions _options;
    private readonly Dictionary<QueueKind, SpeakerQueue> _queues;
    private readonly GraceStore _grace;
    private readonly Dictionary<uint, MumbleUser> _users = new();
    private readonly List<FloorHolder> _floor = [];
    private readonly Dictionary<uint, PendingFloorMove> _pendingMoves = new();
    private readonly HashSet<uint> _speaking = [];
    private readonly LinkedList<AssemblyNoticeDTO> _notices = new();
    private readonly AssemblyPollState _polls;
    private readonly AssemblyFlow _flow = new();
    private uint? ownSession;
    private long sequence;
    private AssemblyLogEntry? pendingRecovery;

    public long Version { get; private set; }
    public string? ItemTitle { get; private set; }
    public int? OpenSlidesAgendaItemId { get; private set; }
    public AssemblyItemKind ItemKind { get; private set; }
    public int? MotionSpeechMinutes { get; private set; }
    public DateTime? ListClosedAt { get; private set; }
    public DateTime? ListClosesAt { get; private set; }
    public MumbleConnectionState MumbleState { get; set; } = MumbleConnectionState.Disconnected;
    public string? MumbleError { get; set; }
    public OpenSlidesMeetingView OpenSlidesView => _polls.View;
    public bool AutoStartPolls => _polls.Settings.AutoStart;

    public AssemblySessionState(Guid sessionId, IReadOnlyDictionary<ChannelRole, uint> channels, AssemblySessionOptions options, AssemblyPollState? polls = null) {
        _sessionId = sessionId;
        _polls = polls ?? new AssemblyPollState(AssemblyPollConfig.Disabled(PollSettings.Default));
        _channels = channels;
        _options = options;
        _roleByChannel = channels
            .GroupBy(c => c.Value)
            .ToDictionary(g => g.Key, g => g.First().Key);
        _queues = Enum.GetValues<QueueKind>().ToDictionary(k => k, k => new SpeakerQueue(k));
        _grace = new GraceStore(options.Grace);
    }

    /// <summary>The open speech of the last run, so its timer resumes after a restart instead of starting from zero.</summary>
    public void PrepareRecovery(AssemblyLogEntry? openSpeech) {
        pendingRecovery = openSpeech;
    }

    public AssemblyEffects OnSynced(MumbleServerSnapshot snapshot, DateTime nowUtc) {
        var effects = new EffectsBuilder();
        ownSession = snapshot.OwnSession;
        _users.Clear();
        foreach (var user in snapshot.Users.Where(u => u.Session != ownSession))
            _users[user.Session] = user;

        ReconcileFloor(nowUtc, effects);
        foreach (var (role, kind) in QueueKindByRole)
            ReconcileQueue(role, kind, nowUtc);

        _pendingMoves.Clear();
        _speaking.RemoveWhere(s => !_users.ContainsKey(s));
        pendingRecovery = null;
        return Structural(effects);
    }

    public AssemblyEffects OnUserEntered(MumbleUser user, uint? previousChannelId, DateTime nowUtc) {
        if (user.Session == ownSession)
            return AssemblyEffects.None;

        var effects = new EffectsBuilder();
        _users[user.Session] = user;
        var newRole = RoleOf(user.ChannelId);
        var previousRole = previousChannelId == null ? null : RoleOf(previousChannelId.Value);

        var floorChanged = IsFloorRole(previousRole);
        if (floorChanged)
            EndFloor(user.Session, nowUtc, effects);

        if (IsFloorRole(newRole)) {
            ArriveOnFloor(user, newRole!.Value, nowUtc, effects);
            return Structural(effects);
        }

        var previousKind = QueueKindOf(previousRole);
        if (previousKind != null)
            LeaveQueue(user.Session, previousKind.Value, nowUtc);

        var newKind = QueueKindOf(newRole);
        if (newKind != null)
            JoinQueue(user, newKind.Value, nowUtc, uncertain: false);

        return floorChanged ? Structural(effects) : Published(effects);
    }

    public AssemblyEffects OnUserDisconnected(MumbleUser user, DateTime nowUtc) {
        if (user.Session == ownSession)
            return AssemblyEffects.None;

        var effects = new EffectsBuilder();
        var floorChanged = FloorHolderOf(user.Session) != null || _pendingMoves.ContainsKey(user.Session);
        EndFloor(user.Session, nowUtc, effects);
        foreach (var queue in _queues.Values)
            LeaveQueue(user.Session, queue.Kind, nowUtc);

        _users.Remove(user.Session);
        _pendingMoves.Remove(user.Session);
        _speaking.Remove(user.Session);
        return floorChanged ? Structural(effects) : Published(effects);
    }

    public AssemblyEffects OnSpeaking(uint session, bool started, DateTime nowUtc) {
        var effects = new EffectsBuilder();
        var changed = started ? _speaking.Add(session) : _speaking.Remove(session);

        var holder = FloorHolderOf(session);
        if (started && holder != null && !holder.Timer.IsStarted && holder.DoneAt == null) {
            holder.Timer.Start(nowUtc);
            effects.Log.Add(SpeechLog(holder, nowUtc, ended: false));
            changed = true;
        }

        effects.Changed = changed;
        return effects.Build();
    }

    public AssemblyEffects OnMoveFailed(uint session, MumbleMoveResult result, DateTime nowUtc) {
        var effects = new EffectsBuilder();
        var name = _users.TryGetValue(session, out var user) ? user.Name : session.ToString();

        if (_pendingMoves.Remove(session, out var pending) && pending.EntryKey != null) {
            var entry = FindEntry(pending.EntryKey);
            if (entry != null)
                entry.MovePending = false;
        }

        var holder = FloorHolderOf(session);
        if (holder != null)
            holder.DoneAt = null;

        Notice(nowUtc, I18nParams.With(I18nKey.Ui.AssemblyConsole.Notice.MoveFailed, ("name", name), ("reason", result.Detail ?? result.Outcome.ToString())));
        return Structural(effects);
    }

    public AssemblyEffects OnNotice(string messageKey, DateTime nowUtc) {
        Notice(nowUtc, messageKey);
        var effects = new EffectsBuilder { Changed = true };
        return effects.Build();
    }

    /// <summary>Ends every open speech when the session ends, so no speech stays open in the log.</summary>
    public AssemblyEffects Close(DateTime nowUtc) {
        var effects = new EffectsBuilder();
        foreach (var holder in _floor.ToList())
            EndFloor(holder.Session, nowUtc, effects);

        return Structural(effects);
    }

    public AssemblyEffects Tick(DateTime nowUtc) {
        var effects = new EffectsBuilder();
        _grace.Expire(nowUtc);

        if (ListClosesAt != null && ListClosesAt.Value <= nowUtc) {
            ListClosedAt = ListClosesAt;
            ListClosesAt = null;
            MarkLateEntries();
            Notice(nowUtc, I18nKey.Ui.AssemblyConsole.Notice.ListClosed);
            Version++;
            effects.Changed = true;
        }

        foreach (var holder in _floor.Where(h => h.DoneAt == null))
            CheckWarnings(holder, nowUtc, effects);

        effects.OpenSlides.AddRange(_polls.Tick(nowUtc));
        return effects.Build();
    }

    public AssemblyEffects OnOpenSlidesView(OpenSlidesMeetingView view, DateTime nowUtc) {
        var effects = new EffectsBuilder { Changed = true };
        var (actions, published) = _polls.OnView(view, nowUtc);
        effects.OpenSlides.AddRange(actions);

        foreach (var poll in published) {
            Notice(nowUtc, I18nParams.With(I18nKey.Ui.AssemblyConsole.Notice.PollPublished, ("title", poll.Title)));
            // Detail = poll title, a newline, then the evaluation's i18n key; the log export splits them again.
            var entry = LogEntry(AssemblyLogKind.Poll, nowUtc, $"{poll.Title}\n{_polls.Evaluate(poll)?.SummaryKey ?? ""}", null);
            entry.OpenSlidesPollId = poll.Id;
            effects.Log.Add(entry);
        }

        return effects.Build();
    }

    public AssemblyEffects OnOpenSlidesConnection(bool connected, string? error, DateTime nowUtc) {
        var wasConnected = _polls.Connected;
        _polls.SetConnection(connected, error);
        if (connected && !wasConnected)
            Notice(nowUtc, I18nKey.Ui.AssemblyConsole.Notice.OpenSlidesConnected);
        if (!connected && wasConnected)
            Notice(nowUtc, I18nKey.Ui.AssemblyConsole.Notice.OpenSlidesLost);

        var effects = new EffectsBuilder { Changed = true };
        return effects.Build();
    }

    public void RegisterPollEvaluation(int pollId, PollEvaluationKind kind, int threshold) {
        _polls.RegisterEvaluation(pollId, kind, threshold);
    }

    public (AssemblyCommandResult Result, AssemblyEffects Effects) SetPollSettings(PollSettings settings, Guid actor, DateTime nowUtc) {
        if (settings.DurationSeconds < 10 || settings.DurationSeconds > 3600)
            return Reject(I18nKey.Ui.AssemblyConsole.Reject.InvalidTime);

        var effects = new EffectsBuilder();
        _polls.Settings = settings;
        ChairAction(I18nParams.With(I18nKey.Ui.AssemblyConsole.Notice.PollSettingsChanged, ("seconds", settings.DurationSeconds.ToString())),
            actor, nowUtc, effects);
        return Accept(effects);
    }

    public (AssemblyCommandResult Result, AssemblyEffects Effects) ExtendPoll(long version, int seconds, Guid actor, DateTime nowUtc) {
        if (version != Version)
            return Reject(I18nKey.Ui.AssemblyConsole.Reject.Stale);
        if (seconds < 1 || seconds > 600)
            return Reject(I18nKey.Ui.AssemblyConsole.Reject.InvalidTime);

        var actions = _polls.Extend(seconds);
        if (actions.Count == 0)
            return Reject(I18nKey.Ui.AssemblyConsole.Reject.NoRunningPoll);

        var effects = new EffectsBuilder();
        effects.OpenSlides.AddRange(actions);
        ChairAction(I18nParams.With(I18nKey.Ui.AssemblyConsole.Notice.PollExtended, ("seconds", seconds.ToString())), actor, nowUtc, effects);
        return Accept(effects);
    }

    public (AssemblyCommandResult Result, AssemblyEffects Effects) StopPoll(long version, int pollId, Guid actor, DateTime nowUtc) {
        return WithPoll(version, pollId, "started", poll => new StopPollEffect(poll.Id),
            I18nKey.Ui.AssemblyConsole.Notice.PollStopRequested, actor, nowUtc);
    }

    public (AssemblyCommandResult Result, AssemblyEffects Effects) PublishPoll(long version, int pollId, Guid actor, DateTime nowUtc) {
        return WithPoll(version, pollId, "finished", poll => new PublishPollEffect(poll.Id),
            I18nKey.Ui.AssemblyConsole.Notice.PollPublishRequested, actor, nowUtc);
    }

    public (AssemblyCommandResult Result, AssemblyEffects Effects) SelectAgendaItem(long version, int agendaItemId, Guid actor, DateTime nowUtc) {
        var item = _polls.FindAgendaItem(agendaItemId);
        if (item == null)
            return Reject(I18nKey.Ui.AssemblyConsole.Reject.AgendaItemNotFound);

        var kind = item.ContentObjectId.StartsWith("motion/") ? AssemblyItemKind.Motion : AssemblyItemKind.Other;
        return SetItem(version, AssemblyPollState.AgendaLabel(item), item.Id, kind, actor, nowUtc);
    }

    public (AssemblyCommandResult Result, AssemblyEffects Effects) GiveFloor(long version, string entryKey, Guid actor, DateTime nowUtc) {
        if (version != Version)
            return Reject(I18nKey.Ui.AssemblyConsole.Reject.Stale);

        var entry = FindEntry(entryKey);
        if (entry == null)
            return Reject(I18nKey.Ui.AssemblyConsole.Reject.EntryNotFound);
        if (entry.MovePending || _pendingMoves.ContainsKey(entry.Session))
            return Reject(I18nKey.Ui.AssemblyConsole.Reject.MovePending);
        if (!_channels.TryGetValue(ChannelRole.Speaker, out var speakerChannel))
            return Reject(I18nKey.Ui.AssemblyConsole.Reject.ChannelNotMapped);

        var effects = new EffectsBuilder();
        entry.MovePending = true;
        _pendingMoves[entry.Session] = new PendingFloorMove(entry.Key, ChannelRole.Speaker);
        effects.Moves.Add(new AssemblyMove(entry.Session, speakerChannel));
        ChairAction(I18nParams.With(I18nKey.Ui.AssemblyConsole.Notice.GaveFloor, ("name", entry.Identity.Name)), actor, nowUtc, effects);
        return Accept(effects);
    }

    public (AssemblyCommandResult Result, AssemblyEffects Effects) GiveFloorToUser(long version, uint session, ChannelRole target, Guid actor, DateTime nowUtc) {
        if (version != Version)
            return Reject(I18nKey.Ui.AssemblyConsole.Reject.Stale);
        if (target != ChannelRole.Speaker && target != ChannelRole.MotionAuthor)
            return Reject(I18nKey.Ui.AssemblyConsole.Reject.InvalidTarget);
        if (!_users.TryGetValue(session, out var user))
            return Reject(I18nKey.Ui.AssemblyConsole.Reject.UserNotFound);
        if (_pendingMoves.ContainsKey(session))
            return Reject(I18nKey.Ui.AssemblyConsole.Reject.MovePending);
        if (!_channels.TryGetValue(target, out var targetChannel))
            return Reject(I18nKey.Ui.AssemblyConsole.Reject.ChannelNotMapped);

        var effects = new EffectsBuilder();
        var entry = _queues.Values.Select(q => q.FindBySession(session)).FirstOrDefault(e => e != null);
        if (entry != null)
            entry.MovePending = true;

        _pendingMoves[session] = new PendingFloorMove(entry?.Key, target);
        effects.Moves.Add(new AssemblyMove(session, targetChannel));
        ChairAction(I18nParams.With(I18nKey.Ui.AssemblyConsole.Notice.GaveFloor, ("name", user.Name)), actor, nowUtc, effects);
        return Accept(effects);
    }

    public (AssemblyCommandResult Result, AssemblyEffects Effects) Done(long version, uint session, Guid actor, DateTime nowUtc) {
        if (version != Version)
            return Reject(I18nKey.Ui.AssemblyConsole.Reject.Stale);

        var holder = FloorHolderOf(session);
        if (holder == null)
            return Reject(I18nKey.Ui.AssemblyConsole.Reject.NotOnFloor);
        if (holder.DoneAt != null)
            return Reject(I18nKey.Ui.AssemblyConsole.Reject.MovePending);
        if (!_channels.TryGetValue(ChannelRole.Listener, out var listenerChannel))
            return Reject(I18nKey.Ui.AssemblyConsole.Reject.ChannelNotMapped);

        var effects = new EffectsBuilder();
        holder.DoneAt = nowUtc;
        effects.Moves.Add(new AssemblyMove(session, listenerChannel));
        ChairAction(I18nParams.With(I18nKey.Ui.AssemblyConsole.Notice.Done, ("name", holder.Identity.Name)), actor, nowUtc, effects);
        return Accept(effects);
    }

    public (AssemblyCommandResult Result, AssemblyEffects Effects) AddMinute(long version, uint session, Guid actor, DateTime nowUtc) {
        return WithTimedHolder(version, session, holder => {
            holder.Timer.AddSeconds(60, nowUtc);
            return I18nParams.With(I18nKey.Ui.AssemblyConsole.Notice.TimeAdded, ("name", holder.Identity.Name));
        }, actor, nowUtc);
    }

    public (AssemblyCommandResult Result, AssemblyEffects Effects) SetSpeechTime(long version, uint session, int seconds, Guid actor, DateTime nowUtc) {
        if (seconds < 1 || seconds > 3600)
            return Reject(I18nKey.Ui.AssemblyConsole.Reject.InvalidTime);

        return WithHolder(version, session, holder => {
            holder.Timer.SetAllotted(seconds, nowUtc);
            return I18nParams.With(I18nKey.Ui.AssemblyConsole.Notice.TimeSet, ("name", holder.Identity.Name), ("seconds", seconds.ToString()));
        }, actor, nowUtc);
    }

    /// <summary>GO § 15h: a changed speaking time is given in whole minutes and holds until the end of the current motion.</summary>
    public (AssemblyCommandResult Result, AssemblyEffects Effects) SetMotionSpeechMinutes(long version, int? minutes, Guid actor, DateTime nowUtc) {
        if (version != Version)
            return Reject(I18nKey.Ui.AssemblyConsole.Reject.Stale);
        if (minutes != null && (minutes < 1 || minutes > 60))
            return Reject(I18nKey.Ui.AssemblyConsole.Reject.InvalidTime);

        var effects = new EffectsBuilder();
        MotionSpeechMinutes = minutes;
        var message = minutes == null
            ? I18nKey.Ui.AssemblyConsole.Notice.MotionTimeCleared
            : I18nParams.With(I18nKey.Ui.AssemblyConsole.Notice.MotionTimeSet, ("minutes", minutes.Value.ToString()));
        ChairAction(message, actor, nowUtc, effects);
        return Accept(effects);
    }

    public (AssemblyCommandResult Result, AssemblyEffects Effects) PauseTimer(long version, uint session, Guid actor, DateTime nowUtc) {
        return WithTimedHolder(version, session, holder => {
            holder.Timer.Pause(nowUtc);
            return I18nParams.With(I18nKey.Ui.AssemblyConsole.Notice.TimerPaused, ("name", holder.Identity.Name));
        }, actor, nowUtc);
    }

    public (AssemblyCommandResult Result, AssemblyEffects Effects) ResumeTimer(long version, uint session, Guid actor, DateTime nowUtc) {
        return WithTimedHolder(version, session, holder => {
            holder.Timer.Resume(nowUtc);
            return I18nParams.With(I18nKey.Ui.AssemblyConsole.Notice.TimerResumed, ("name", holder.Identity.Name));
        }, actor, nowUtc);
    }

    public (AssemblyCommandResult Result, AssemblyEffects Effects) StartTimerNow(long version, uint session, Guid actor, DateTime nowUtc) {
        var result = WithHolder(version, session, holder => {
            holder.Timer.Start(nowUtc);
            return I18nParams.With(I18nKey.Ui.AssemblyConsole.Notice.TimerStarted, ("name", holder.Identity.Name));
        }, actor, nowUtc);
        if (!result.Result.Accepted)
            return result;

        var started = FloorHolderOf(session)!;
        var log = result.Effects.Log.Append(SpeechLog(started, nowUtc, ended: false)).ToList();
        return (result.Result, result.Effects with { Log = log });
    }

    public (AssemblyCommandResult Result, AssemblyEffects Effects) Skip(long version, string entryKey, bool skipped, Guid actor, DateTime nowUtc) {
        return WithEntry(version, entryKey, entry => {
            entry.Skipped = skipped;
            var key = skipped ? I18nKey.Ui.AssemblyConsole.Notice.Skipped : I18nKey.Ui.AssemblyConsole.Notice.Unskipped;
            return I18nParams.With(key, ("name", entry.Identity.Name));
        }, actor, nowUtc);
    }

    public (AssemblyCommandResult Result, AssemblyEffects Effects) Dismiss(long version, string entryKey, Guid actor, DateTime nowUtc) {
        return WithEntry(version, entryKey, entry => {
            entry.Dismissed = true;
            return I18nParams.With(I18nKey.Ui.AssemblyConsole.Notice.Dismissed, ("name", entry.Identity.Name));
        }, actor, nowUtc);
    }

    public (AssemblyCommandResult Result, AssemblyEffects Effects) UndoDismiss(long version, string entryKey, Guid actor, DateTime nowUtc) {
        return WithEntry(version, entryKey, entry => {
            entry.Dismissed = false;
            return I18nParams.With(I18nKey.Ui.AssemblyConsole.Notice.DismissUndone, ("name", entry.Identity.Name));
        }, actor, nowUtc);
    }

    /// <summary>Closes the list of speakers now, or at <paramref name="atUtc"/> if that lies in the future.</summary>
    public (AssemblyCommandResult Result, AssemblyEffects Effects) CloseList(long version, DateTime? atUtc, Guid actor, DateTime nowUtc) {
        if (version != Version)
            return Reject(I18nKey.Ui.AssemblyConsole.Reject.Stale);

        var effects = new EffectsBuilder();
        if (atUtc == null || atUtc.Value <= nowUtc) {
            ListClosedAt = nowUtc;
            ListClosesAt = null;
            MarkLateEntries();
            ChairAction(I18nKey.Ui.AssemblyConsole.Notice.ListClosed, actor, nowUtc, effects);
        } else {
            ListClosesAt = atUtc;
            ChairAction(I18nParams.With(I18nKey.Ui.AssemblyConsole.Notice.ListClosesAt, ("time", BerlinTime.FromUtc(atUtc.Value).ToString("HH:mm"))), actor, nowUtc, effects);
        }

        return Accept(effects);
    }

    public (AssemblyCommandResult Result, AssemblyEffects Effects) ReopenList(long version, Guid actor, DateTime nowUtc) {
        if (version != Version)
            return Reject(I18nKey.Ui.AssemblyConsole.Reject.Stale);

        var effects = new EffectsBuilder();
        ListClosedAt = null;
        ListClosesAt = null;
        foreach (var entry in _queues.Values.SelectMany(q => q.Entries))
            entry.AfterListClosed = false;

        ChairAction(I18nKey.Ui.AssemblyConsole.Notice.ListReopened, actor, nowUtc, effects);
        return Accept(effects);
    }

    public (AssemblyCommandResult Result, AssemblyEffects Effects) SetItem(long version, string title, int? openSlidesAgendaItemId, AssemblyItemKind kind,
        Guid actor, DateTime nowUtc) {
        if (version != Version)
            return Reject(I18nKey.Ui.AssemblyConsole.Reject.Stale);

        var effects = new EffectsBuilder();
        ItemTitle = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        OpenSlidesAgendaItemId = openSlidesAgendaItemId;
        ItemKind = kind;
        MotionSpeechMinutes = null;
        _flow.Reset(kind);

        var message = I18nParams.With(I18nKey.Ui.AssemblyConsole.Notice.ItemChanged, ("title", ItemTitle ?? ""));
        Notice(nowUtc, message);
        effects.Log.Add(LogEntry(AssemblyLogKind.ItemChange, nowUtc, message, actor));
        return Accept(effects);
    }

    public QueueEntry? Suggestion() {
        return SuggestionEngine.Suggest(_flow, _queues, IsBusy());
    }

    public (AssemblyCommandResult Result, AssemblyEffects Effects) SetPhase(long version, AssemblyPhase phase, Guid actor, DateTime nowUtc) {
        return FlowCommand(version, () => {
            _flow.SetPhase(phase);
            return AssemblyLabels.PhaseKey(phase);
        }, actor, nowUtc);
    }

    public (AssemblyCommandResult Result, AssemblyEffects Effects) StartNextRound(long version, Guid actor, DateTime nowUtc) {
        return FlowCommand(version, () => {
            _flow.StartNextRound();
            return I18nParams.With(I18nKey.Ui.AssemblyConsole.Notice.NextRound, ("round", _flow.Round.ToString()));
        }, actor, nowUtc);
    }

    public (AssemblyCommandResult Result, AssemblyEffects Effects) RecordGoAccepted(long version, Guid actor, DateTime nowUtc) {
        return FlowCommand(version, () => {
            _flow.EndGo();
            return I18nKey.Ui.AssemblyConsole.Notice.GoAccepted;
        }, actor, nowUtc);
    }

    public (AssemblyCommandResult Result, AssemblyEffects Effects) StartGoObjection(long version, Guid actor, DateTime nowUtc) {
        return FlowCommand(version, () => {
            _flow.StartGoObjection();
            return I18nKey.Ui.AssemblyConsole.Notice.GoObjection;
        }, actor, nowUtc);
    }

    public (AssemblyCommandResult Result, AssemblyEffects Effects) ResumeAfterGo(long version, Guid actor, DateTime nowUtc) {
        return FlowCommand(version, () => {
            _flow.EndGo();
            return I18nKey.Ui.AssemblyConsole.Notice.GoEnded;
        }, actor, nowUtc);
    }

    public (AssemblyCommandResult Result, AssemblyEffects Effects) SetElectionRunning(long version, bool running, Guid actor, DateTime nowUtc) {
        return FlowCommand(version, () => {
            _flow.ElectionRunning = running;
            return running ? I18nKey.Ui.AssemblyConsole.Notice.ElectionStarted : I18nKey.Ui.AssemblyConsole.Notice.ElectionStopped;
        }, actor, nowUtc);
    }

    public (AssemblyCommandResult Result, AssemblyEffects Effects) SetCandidates(long version, IReadOnlyList<string> names, Guid actor, DateTime nowUtc) {
        return FlowCommand(version, () => {
            _flow.SetCandidates(names);
            return I18nParams.With(I18nKey.Ui.AssemblyConsole.Notice.CandidatesSet, ("count", _flow.Candidates.Count.ToString()));
        }, actor, nowUtc);
    }

    public (AssemblyCommandResult Result, AssemblyEffects Effects) SetCurrentCandidate(long version, int? index, Guid actor, DateTime nowUtc) {
        if (version != Version)
            return Reject(I18nKey.Ui.AssemblyConsole.Reject.Stale);
        if (!_flow.SetCurrentCandidate(index))
            return Reject(I18nKey.Ui.AssemblyConsole.Reject.EntryNotFound);
        // Choosing a candidate starts their presentation, in one command so a second click cannot be stale.
        if (index != null)
            _flow.SetPhase(AssemblyPhase.CandidatePresentation);

        var effects = new EffectsBuilder();
        var name = index == null ? "-" : _flow.Candidates[index.Value];
        ChairAction(I18nParams.With(I18nKey.Ui.AssemblyConsole.Notice.CurrentCandidate, ("name", name)), actor, nowUtc, effects);
        return Accept(effects);
    }

    /// <summary>What the poll templates need to plan a poll on the current agenda item.</summary>
    public PollPlanContext GetPollPlanContext(int meetingId) {
        var item = OpenSlidesAgendaItemId == null ? null : _polls.FindAgendaItem(OpenSlidesAgendaItemId.Value);
        var label = item == null ? "" : item.MotionNumber != null ? $"{item.MotionNumber} {item.Title}" : item.Title;
        return new PollPlanContext(meetingId, _polls.DelegatesGroupId, item, _polls.View, _polls.CompetitorsOf(item), label);
    }

    public AssemblyEffects RegisterCreatedPoll(int pollId, PollPlan plan, Guid actor, DateTime nowUtc) {
        _polls.MarkCreatedHere(pollId);
        if (plan.Kind != null)
            _polls.RegisterEvaluation(pollId, plan.Kind.Value, plan.Threshold);

        var effects = new EffectsBuilder();
        ChairAction(I18nParams.With(I18nKey.Ui.AssemblyConsole.Notice.PollCreated, ("title", plan.Title)), actor, nowUtc, effects);
        effects.Changed = true;
        return effects.Build();
    }

    public AssemblyEffects OnProceduralTopicsCreated(int count, Guid actor, DateTime nowUtc) {
        var effects = new EffectsBuilder();
        ChairAction(I18nParams.With(I18nKey.Ui.AssemblyConsole.Notice.ProceduralTopicsCreated, ("count", count.ToString())), actor, nowUtc, effects);
        effects.Changed = true;
        return effects.Build();
    }

    public (AssemblyCommandResult Result, AssemblyEffects Effects) ResetPoll(long version, int pollId, Guid actor, DateTime nowUtc) {
        return WithOwnPoll(version, pollId, poll => new ResetPollEffect(poll.Id), I18nKey.Ui.AssemblyConsole.Notice.PollReset, actor, nowUtc);
    }

    public (AssemblyCommandResult Result, AssemblyEffects Effects) DeletePoll(long version, int pollId, Guid actor, DateTime nowUtc) {
        return WithOwnPoll(version, pollId, poll => new DeletePollEffect(poll.Id), I18nKey.Ui.AssemblyConsole.Notice.PollDeleted, actor, nowUtc);
    }

    public (AssemblyCommandResult Result, AssemblyEffects Effects) SetRepeatPresentation(long version, bool repeat, Guid actor, DateTime nowUtc) {
        return FlowCommand(version, () => {
            _flow.RepeatPresentation = repeat;
            return repeat ? I18nKey.Ui.AssemblyConsole.Notice.RepeatPresentationOn : I18nKey.Ui.AssemblyConsole.Notice.RepeatPresentationOff;
        }, actor, nowUtc);
    }

    public AssemblySnapshotDTO Snapshot(DateTime nowUtc) {
        return new AssemblySnapshotDTO {
            Version = Version,
            ServerNowUtc = nowUtc,
            SessionId = _sessionId,
            MumbleState = MumbleState.ToString(),
            MumbleError = MumbleError,
            ItemTitle = ItemTitle,
            ItemKind = ItemKind,
            MotionSpeechMinutes = MotionSpeechMinutes,
            ListClosedAt = ListClosedAt,
            ListClosesAt = ListClosesAt,
            SuggestedKey = Suggestion()?.Key,
            Queues = _queues.Values
                .OrderBy(q => q.Kind)
                .Select(q => new AssemblyQueueDTO {
                    Kind = q.Kind,
                    ChannelMapped = _channels.ContainsKey(RoleOf(q.Kind)),
                    Entries = q.Entries.Select(e => e.ToDto()).ToList()
                })
                .ToList(),
            Floor = _floor.Select(h => h.ToDto(nowUtc, _options.StartNowAfter)).ToList(),
            Users = _users.Values
                .OrderBy(u => u.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(u => new AssemblyUserDTO { Session = u.Session, Name = u.Name, ChannelRole = RoleOf(u.ChannelId) })
                .ToList(),
            SpeakingSessions = _speaking.OrderBy(s => s).ToList(),
            Phase = _flow.Phase,
            Round = _flow.Round,
            ProCount = _flow.ProCount,
            ContraCount = _flow.ContraCount,
            SpeechesPerSide = AssemblyFlow.SpeechesPerSide,
            RoundComplete = SuggestionEngine.IsRoundComplete(_flow, _queues),
            WaitingCount = _queues[QueueKind.Pro].Entries.Count(e => e.IsSuggestable()) + _queues[QueueKind.Contra].Entries.Count(e => e.IsSuggestable()),
            GoActive = _flow.GoActive,
            GoObjection = _flow.GoObjection,
            RepeatPresentation = _flow.RepeatPresentation,
            Questioners = _flow.Questioners,
            MaxQuestioners = AssemblyFlow.MaxQuestioners,
            ElectionRunning = _flow.ElectionRunning,
            CurrentIsMotion = ItemKind == AssemblyItemKind.Motion,
            HasCompetitors = OpenSlidesAgendaItemId != null && _polls.CompetitorsOf(_polls.FindAgendaItem(OpenSlidesAgendaItemId.Value)).Count > 0,
            ProjectorConfigured = _polls.ProjectorIds.Count > 0,
            Candidates = _flow.Candidates.ToList(),
            CurrentCandidate = _flow.CurrentCandidate,
            Notices = _notices.ToList(),
            OpenSlidesState = _polls.StateName,
            OpenSlidesError = _polls.Error,
            CurrentAgendaItemId = OpenSlidesAgendaItemId,
            PollSettings = new AssemblyPollSettingsDTO {
                DurationSeconds = _polls.Settings.DurationSeconds,
                AutoStart = _polls.Settings.AutoStart,
                AutoStop = _polls.Settings.AutoStop,
                AutoPublish = _polls.Settings.AutoPublish,
                Project = _polls.Settings.Project
            },
            Polls = _polls.PollDtos(),
            AgendaItems = _polls.AgendaDtos()
        };
    }

    public AssemblyOverlayDTO OverlaySnapshot(DateTime nowUtc) {
        var holder = _floor.LastOrDefault(h => h.Channel == ChannelRole.Speaker && h.DoneAt == null)
            ?? _floor.LastOrDefault(h => h.Channel == ChannelRole.MotionAuthor && h.DoneAt == null);

        return new AssemblyOverlayDTO {
            ServerNowUtc = nowUtc,
            Active = true,
            ItemTitle = ItemTitle,
            SpeakerName = holder?.Identity.Name,
            SpeakerRole = holder?.Role,
            Timer = holder?.Timer.ToDto(nowUtc, false),
            Poll = _polls.OverlayPoll(nowUtc),
            NextPro = SuggestionEngine.NextNames(_queues[QueueKind.Pro], 2),
            NextContra = SuggestionEngine.NextNames(_queues[QueueKind.Contra], 2),
            ElectionRunning = _flow.ElectionRunning
        };
    }

    private void ReconcileFloor(DateTime nowUtc, EffectsBuilder effects) {
        foreach (var holder in _floor.ToList()) {
            var present = _users.Values.FirstOrDefault(u => MemberIdentity.Of(u).Key == holder.Identity.Key && RoleOf(u.ChannelId) == holder.Channel);
            if (present == null) {
                EndFloor(holder.Session, nowUtc, effects);
            } else {
                holder.Session = present.Session;
            }
        }

        foreach (var user in _users.Values.Where(u => IsFloorRole(RoleOf(u.ChannelId))).OrderBy(u => u.Session)) {
            if (_floor.Any(h => h.Session == user.Session))
                continue;

            var role = RoleOf(user.ChannelId)!.Value;
            var speakerRole = role == ChannelRole.MotionAuthor ? MotionAuthorRole() : SpeakerRole.Manual;
            _floor.Add(new FloorHolder(user.Session, MemberIdentity.Of(user), role, speakerRole, nowUtc, RecoveredTimer(user, speakerRole, role)));
        }
    }

    private SpeakingTimer RecoveredTimer(MumbleUser user, SpeakerRole role, ChannelRole channel) {
        var recovery = pendingRecovery;
        if (recovery?.StartedAt != null && recovery.MumbleName == user.Name)
            return SpeakingTimer.Restored(recovery.AllottedSeconds, recovery.StartedAt.Value);

        return new SpeakingTimer(AllottedFor(role, channel));
    }

    private void ReconcileQueue(ChannelRole role, QueueKind kind, DateTime nowUtc) {
        var queue = _queues[kind];
        var present = _channels.TryGetValue(role, out var channelId)
            ? _users.Values.Where(u => u.ChannelId == channelId).OrderBy(u => u.Session).ToList()
            : [];

        foreach (var entry in queue.Entries.ToList()) {
            var user = present.FirstOrDefault(u => MemberIdentity.Of(u).Key == entry.Identity.Key);
            if (user == null) {
                queue.Remove(entry);
                _grace.Park(entry, nowUtc);
            } else {
                entry.Session = user.Session;
                entry.MovePending = false;
            }
        }

        foreach (var user in present) {
            if (queue.FindBySession(user.Session) == null)
                JoinQueue(user, kind, nowUtc, uncertain: true);
        }
    }

    private void JoinQueue(MumbleUser user, QueueKind kind, DateTime nowUtc, bool uncertain) {
        var queue = _queues[kind];
        var identity = MemberIdentity.Of(user);
        var key = QueueEntry.KeyFor(kind, identity);

        var existing = queue.FindByKey(key);
        if (existing != null) {
            existing.Session = user.Session;
            return;
        }

        var entry = _grace.TryRestore(key, nowUtc) ?? new QueueEntry(kind, identity, user.Session, nowUtc, ++sequence) {
            OrderUncertain = uncertain,
            AfterListClosed = ListClosedAt != null
        };
        entry.Session = user.Session;
        entry.Identity = identity;
        queue.Add(entry);
    }

    private void LeaveQueue(uint session, QueueKind kind, DateTime nowUtc) {
        var queue = _queues[kind];
        var entry = queue.FindBySession(session);
        if (entry == null)
            return;

        queue.Remove(entry);
        if (!entry.Dismissed)
            _grace.Park(entry, nowUtc);
    }

    private void ArriveOnFloor(MumbleUser user, ChannelRole channel, DateTime nowUtc, EffectsBuilder effects) {
        _pendingMoves.Remove(user.Session, out var pending);

        // A request is consumed by getting the floor, whether the bot moved the member or someone did it by hand in Mumble.
        QueueEntry? consumed = null;
        foreach (var queue in _queues.Values) {
            consumed = queue.FindBySession(user.Session);
            if (consumed != null) {
                queue.Remove(consumed);
                break;
            }
        }

        var role = channel == ChannelRole.MotionAuthor
            ? MotionAuthorRole()
            : consumed != null ? SpeakerRoleOf(consumed.Kind) : SpeakerRole.Manual;
        _flow.OnFloorArrival(role);
        _floor.Add(new FloorHolder(user.Session, MemberIdentity.Of(user), channel, role, nowUtc, new SpeakingTimer(AllottedFor(role, channel))));

        if (pending == null)
            Notice(nowUtc, I18nParams.With(I18nKey.Ui.AssemblyConsole.Notice.ManualFloor, ("name", user.Name)));
    }

    private void EndFloor(uint session, DateTime nowUtc, EffectsBuilder effects) {
        var holder = FloorHolderOf(session);
        if (holder == null)
            return;

        _floor.Remove(holder);
        effects.Log.Add(SpeechLog(holder, holder.DoneAt ?? nowUtc, ended: true));
    }

    private void CheckWarnings(FloorHolder holder, DateTime nowUtc, EffectsBuilder effects) {
        var timer = holder.Timer;
        if (!timer.IsStarted || timer.IsPaused)
            return;

        var remaining = timer.Remaining(nowUtc);
        if (remaining == null)
            return;

        if (IsWarningDue(timer, remaining.Value)) {
            timer.WarningSent = true;
            effects.Texts.Add(new AssemblyText(holder.Session, _options.WarningText));
            effects.Changed = true;
        }

        if (!timer.ExpiredSent && remaining.Value <= TimeSpan.Zero) {
            timer.ExpiredSent = true;
            timer.WarningSent = true;
            effects.Texts.Add(new AssemblyText(holder.Session, _options.ExpiredText));
            effects.Changed = true;
        }
    }

    // A speech shorter than the warning lead time gets no warning, only the expiry text.
    private static bool IsWarningDue(SpeakingTimer timer, TimeSpan remaining) {
        if (timer.WarningSent)
            return false;
        if (timer.AllottedSeconds <= SpeakingTimer.WarningSeconds)
            return false;

        return remaining.TotalSeconds <= SpeakingTimer.WarningSeconds;
    }

    private int? AllottedFor(SpeakerRole role, ChannelRole channel) {
        if (channel == ChannelRole.MotionAuthor)
            return MotionAuthorTime();

        return role switch {
            SpeakerRole.Point => null,
            SpeakerRole.Question => _flow.Phase == AssemblyPhase.CandidateQuestions ? _options.Times.CandidateQuestion : _options.Times.Question,
            _ => MotionSpeechMinutes * 60 ?? _options.Times.Speech
        };
    }

    // The motion author answers questions without a time limit (GO § 12 (3)); candidates get 2 minutes per answer (GO § 11).
    private int? MotionAuthorTime() {
        var times = _options.Times;
        return _flow.Phase switch {
            AssemblyPhase.Questions => null,
            AssemblyPhase.Statement => times.Statement,
            AssemblyPhase.ClosingWord => times.ClosingWord,
            AssemblyPhase.CandidatePresentation => _flow.RepeatPresentation ? times.CandidatePresentationRepeat : times.CandidatePresentation,
            AssemblyPhase.CandidateQuestions => times.CandidateAnswer,
            AssemblyPhase.Debate => MotionSpeechMinutes * 60 ?? times.Speech,
            _ => times.Presentation
        };
    }

    // Nobody may be interrupted by a GO motion or a question: neither a running speech nor a running poll.
    private bool IsBusy() {
        if (_floor.Any(h => h.Channel == ChannelRole.Speaker && h.DoneAt == null))
            return true;

        return _polls.View.Polls.Any(p => p.State == "started");
    }

    private (AssemblyCommandResult Result, AssemblyEffects Effects) FlowCommand(long version, Func<string> change, Guid actor, DateTime nowUtc) {
        if (version != Version)
            return Reject(I18nKey.Ui.AssemblyConsole.Reject.Stale);

        var effects = new EffectsBuilder();
        var message = change();
        Notice(nowUtc, message);
        effects.Log.Add(LogEntry(AssemblyLogKind.PhaseChange, nowUtc, message, actor));
        return Accept(effects);
    }

    // Members already waiting when the list closes keep their request; only entries after the closing time are late.
    private void MarkLateEntries() {
        if (ListClosedAt == null)
            return;

        foreach (var entry in _queues.Values.SelectMany(q => q.Entries).Where(e => e.EnteredAt > ListClosedAt.Value))
            entry.AfterListClosed = true;
    }

    private (AssemblyCommandResult Result, AssemblyEffects Effects) WithHolder(long version, uint session, Func<FloorHolder, string> change, Guid actor, DateTime nowUtc) {
        if (version != Version)
            return Reject(I18nKey.Ui.AssemblyConsole.Reject.Stale);

        var holder = FloorHolderOf(session);
        if (holder == null)
            return Reject(I18nKey.Ui.AssemblyConsole.Reject.NotOnFloor);

        var effects = new EffectsBuilder();
        ChairAction(change(holder), actor, nowUtc, effects);
        return Accept(effects);
    }

    private (AssemblyCommandResult Result, AssemblyEffects Effects) WithTimedHolder(long version, uint session, Func<FloorHolder, string> change, Guid actor, DateTime nowUtc) {
        var holder = FloorHolderOf(session);
        if (holder != null && holder.Timer.AllottedSeconds == null)
            return Reject(I18nKey.Ui.AssemblyConsole.Reject.Untimed);

        return WithHolder(version, session, change, actor, nowUtc);
    }

    private (AssemblyCommandResult Result, AssemblyEffects Effects) WithEntry(long version, string entryKey, Func<QueueEntry, string> change, Guid actor, DateTime nowUtc) {
        if (version != Version)
            return Reject(I18nKey.Ui.AssemblyConsole.Reject.Stale);

        var entry = FindEntry(entryKey);
        if (entry == null)
            return Reject(I18nKey.Ui.AssemblyConsole.Reject.EntryNotFound);

        var effects = new EffectsBuilder();
        ChairAction(change(entry), actor, nowUtc, effects);
        return Accept(effects);
    }

    private (AssemblyCommandResult Result, AssemblyEffects Effects) Accept(EffectsBuilder effects) {
        return (AssemblyCommandResult.Ok(), Structural(effects));
    }

    private static (AssemblyCommandResult Result, AssemblyEffects Effects) Reject(string messageKey) {
        return (AssemblyCommandResult.Rejected(messageKey), AssemblyEffects.None);
    }

    private AssemblyEffects Structural(EffectsBuilder effects) {
        Version++;
        return Published(effects);
    }

    private static AssemblyEffects Published(EffectsBuilder effects) {
        effects.Changed = true;
        return effects.Build();
    }

    private void ChairAction(string messageKey, Guid actor, DateTime nowUtc, EffectsBuilder effects) {
        Notice(nowUtc, messageKey);
        effects.Log.Add(LogEntry(AssemblyLogKind.ChairAction, nowUtc, messageKey, actor));
    }

    private void Notice(DateTime nowUtc, string messageKey) {
        _notices.AddFirst(new AssemblyNoticeDTO { At = nowUtc, MessageKey = messageKey });
        while (_notices.Count > MaxNotices)
            _notices.RemoveLast();
    }

    private AssemblyLogEntry LogEntry(AssemblyLogKind kind, DateTime nowUtc, string detail, Guid? actor) {
        return new AssemblyLogEntry {
            SessionId = _sessionId,
            At = nowUtc,
            Kind = kind,
            ItemTitle = ItemTitle,
            OpenSlidesAgendaItemId = OpenSlidesAgendaItemId,
            Detail = detail,
            ActorUserId = actor
        };
    }

    private AssemblyLogEntry SpeechLog(FloorHolder holder, DateTime atUtc, bool ended) {
        return new AssemblyLogEntry {
            Id = holder.LogEntryId,
            SessionId = _sessionId,
            At = holder.Timer.StartedAt ?? holder.ArrivedAt,
            Kind = AssemblyLogKind.Speech,
            ItemTitle = ItemTitle,
            OpenSlidesAgendaItemId = OpenSlidesAgendaItemId,
            MumbleName = holder.Identity.Name,
            Role = holder.Role.ToString(),
            StartedAt = holder.Timer.StartedAt,
            EndedAt = ended ? atUtc : null,
            AllottedSeconds = holder.Timer.AllottedSeconds
        };
    }

    private QueueEntry? FindEntry(string key) {
        return _queues.Values.Select(q => q.FindByKey(key)).FirstOrDefault(e => e != null);
    }

    private FloorHolder? FloorHolderOf(uint session) {
        return _floor.FirstOrDefault(h => h.Session == session);
    }

    private ChannelRole? RoleOf(uint channelId) {
        return _roleByChannel.TryGetValue(channelId, out var role) ? role : null;
    }

    private static ChannelRole RoleOf(QueueKind kind) {
        return QueueKindByRole.First(p => p.Value == kind).Key;
    }

    private static QueueKind? QueueKindOf(ChannelRole? role) {
        if (role == null)
            return null;

        return QueueKindByRole.TryGetValue(role.Value, out var kind) ? kind : null;
    }

    private static bool IsFloorRole(ChannelRole? role) {
        return role == ChannelRole.Speaker || role == ChannelRole.MotionAuthor;
    }

    private static SpeakerRole SpeakerRoleOf(QueueKind kind) {
        return kind switch {
            QueueKind.Point => SpeakerRole.Point,
            QueueKind.Question => SpeakerRole.Question,
            QueueKind.Pro => SpeakerRole.Pro,
            _ => SpeakerRole.Contra
        };
    }

    private record PendingFloorMove(string? EntryKey, ChannelRole Target);

    // During an election the person in the motion author channel is a candidate (GO § 11).
    private SpeakerRole MotionAuthorRole() {
        return ItemKind == AssemblyItemKind.Election || AssemblyFlow.IsElectionPhase(_flow.Phase) ? SpeakerRole.Candidate : SpeakerRole.MotionAuthor;
    }

    private (AssemblyCommandResult Result, AssemblyEffects Effects) WithOwnPoll(long version, int pollId, Func<OpenSlidesPollView, OpenSlidesEffect> action,
        string noticeKey, Guid actor, DateTime nowUtc) {
        if (version != Version)
            return Reject(I18nKey.Ui.AssemblyConsole.Reject.Stale);
        if (!_polls.IsCreatedHere(pollId))
            return Reject(I18nKey.Ui.AssemblyConsole.Reject.NotOwnPoll);

        var poll = _polls.FindPoll(pollId);
        if (poll == null)
            return Reject(I18nKey.Ui.AssemblyConsole.Reject.PollNotFound);

        var effects = new EffectsBuilder();
        effects.OpenSlides.Add(action(poll));
        ChairAction(I18nParams.With(noticeKey, ("title", poll.Title)), actor, nowUtc, effects);
        return Accept(effects);
    }

    private (AssemblyCommandResult Result, AssemblyEffects Effects) WithPoll(long version, int pollId, string requiredState,
        Func<OpenSlidesPollView, OpenSlidesEffect> action, string noticeKey, Guid actor, DateTime nowUtc) {
        if (version != Version)
            return Reject(I18nKey.Ui.AssemblyConsole.Reject.Stale);

        var poll = _polls.FindPoll(pollId);
        if (poll == null || poll.State != requiredState)
            return Reject(I18nKey.Ui.AssemblyConsole.Reject.PollNotFound);

        var effects = new EffectsBuilder();
        effects.OpenSlides.Add(action(poll));
        ChairAction(I18nParams.With(noticeKey, ("title", poll.Title)), actor, nowUtc, effects);
        return Accept(effects);
    }

    private class EffectsBuilder {
        public List<OpenSlidesEffect> OpenSlides { get; } = [];
        public List<AssemblyMove> Moves { get; } = [];
        public List<AssemblyText> Texts { get; } = [];
        public List<AssemblyLogEntry> Log { get; } = [];
        public bool Changed { get; set; }

        public AssemblyEffects Build() {
            return new AssemblyEffects(Moves, Texts, Log, Changed) { OpenSlides = OpenSlides };
        }
    }
}
