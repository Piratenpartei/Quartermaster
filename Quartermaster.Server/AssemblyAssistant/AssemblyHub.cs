using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Quartermaster.Api;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Api.I18n;
using Quartermaster.Data.Events;
using Quartermaster.Server.AssemblyAssistant.OpenSlides;
using Quartermaster.Server.AssemblyAssistant.Templates;
using Quartermaster.Server.AssemblyAssistant.Session;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.AssemblyAssistant;

/// <summary>
/// Real-time hub for the assembly console and the OBS overlay. Anonymous connections are allowed so the overlay works as a
/// browser source without login; they only get <see cref="AssemblyOverlayDTO"/>. The console needs <c>assembly_view</c>,
/// every command <c>assembly_chair</c> on the event's chapter, and each command carries the state version it was issued against.
/// </summary>
public class AssemblyHub : Hub {
    public const string ConsoleGroupPrefix = "assembly:console:";
    public const string OverlayGroupPrefix = "assembly:overlay:";

    private readonly PermissionContext _perms;
    private readonly EventRepository _events;
    private readonly AssemblySessionHost _sessions;

    public AssemblyHub(PermissionContext perms, EventRepository events, AssemblySessionHost sessions) {
        _perms = perms;
        _events = events;
        _sessions = sessions;
    }

    public static string ConsoleGroup(Guid eventId) {
        return ConsoleGroupPrefix + eventId;
    }

    public static string OverlayGroup(Guid eventId) {
        return OverlayGroupPrefix + eventId;
    }

    public async Task<AssemblyConsoleJoinDTO> JoinConsole(Guid eventId) {
        _perms.Bind(Context.User!);
        var evt = _events.Get(eventId);
        if (evt == null || _perms.UserId == null)
            return new AssemblyConsoleJoinDTO();

        var canControl = _perms.Has(evt.ChapterId, PermissionIdentifier.AssemblyChair);
        if (!canControl && !_perms.Has(evt.ChapterId, PermissionIdentifier.AssemblyView))
            return new AssemblyConsoleJoinDTO();

        await Groups.AddToGroupAsync(Context.ConnectionId, ConsoleGroup(eventId));
        var snapshot = _sessions.EventId == eventId ? _sessions.Snapshot() : null;
        return new AssemblyConsoleJoinDTO {
            Allowed = true,
            CanControl = canControl,
            EventName = evt.PublicName,
            Snapshot = snapshot
        };
    }

    public async Task<AssemblyOverlayDTO> JoinOverlay(Guid eventId) {
        await Groups.AddToGroupAsync(Context.ConnectionId, OverlayGroup(eventId));
        return _sessions.OverlaySnapshot(eventId);
    }

    public Task<AssemblyCommandResult> GiveFloor(Guid eventId, long version, string entryKey) {
        return Command(eventId, (state, now, actor) => state.GiveFloor(version, entryKey, actor, now));
    }

    public Task<AssemblyCommandResult> GiveFloorToUser(Guid eventId, long version, uint session, ChannelRole target) {
        return Command(eventId, (state, now, actor) => state.GiveFloorToUser(version, session, target, actor, now));
    }

    public Task<AssemblyCommandResult> Done(Guid eventId, long version, uint session) {
        return Command(eventId, (state, now, actor) => state.Done(version, session, actor, now));
    }

    public Task<AssemblyCommandResult> AddMinute(Guid eventId, long version, uint session) {
        return Command(eventId, (state, now, actor) => state.AddMinute(version, session, actor, now));
    }

    public Task<AssemblyCommandResult> SetSpeechTime(Guid eventId, long version, uint session, int seconds) {
        return Command(eventId, (state, now, actor) => state.SetSpeechTime(version, session, seconds, actor, now));
    }

    public Task<AssemblyCommandResult> SetMotionSpeechMinutes(Guid eventId, long version, int? minutes) {
        return Command(eventId, (state, now, actor) => state.SetMotionSpeechMinutes(version, minutes, actor, now));
    }

    public Task<AssemblyCommandResult> PauseTimer(Guid eventId, long version, uint session) {
        return Command(eventId, (state, now, actor) => state.PauseTimer(version, session, actor, now));
    }

    public Task<AssemblyCommandResult> ResumeTimer(Guid eventId, long version, uint session) {
        return Command(eventId, (state, now, actor) => state.ResumeTimer(version, session, actor, now));
    }

    public Task<AssemblyCommandResult> StartTimerNow(Guid eventId, long version, uint session) {
        return Command(eventId, (state, now, actor) => state.StartTimerNow(version, session, actor, now));
    }

    public Task<AssemblyCommandResult> Skip(Guid eventId, long version, string entryKey, bool skipped) {
        return Command(eventId, (state, now, actor) => state.Skip(version, entryKey, skipped, actor, now));
    }

    public Task<AssemblyCommandResult> Dismiss(Guid eventId, long version, string entryKey) {
        return Command(eventId, (state, now, actor) => state.Dismiss(version, entryKey, actor, now));
    }

    public Task<AssemblyCommandResult> UndoDismiss(Guid eventId, long version, string entryKey) {
        return Command(eventId, (state, now, actor) => state.UndoDismiss(version, entryKey, actor, now));
    }

    public Task<AssemblyCommandResult> CloseList(Guid eventId, long version, DateTime? atUtc) {
        return Command(eventId, (state, now, actor) => state.CloseList(version, atUtc, actor, now));
    }

    public Task<AssemblyCommandResult> ReopenList(Guid eventId, long version) {
        return Command(eventId, (state, now, actor) => state.ReopenList(version, actor, now));
    }

    public Task<AssemblyCommandResult> SetItem(Guid eventId, long version, string title) {
        return Command(eventId, (state, now, actor) => state.SetItem(version, title, null, AssemblyItemKind.Other, actor, now));
    }

    public Task<AssemblyCommandResult> SetPollSettings(Guid eventId, int durationSeconds, bool autoStart, bool autoStop, bool autoPublish, bool project) {
        return Command(eventId, (state, now, actor) =>
            state.SetPollSettings(new PollSettings(durationSeconds, autoStart, autoStop, autoPublish, project), actor, now));
    }

    public async Task<AssemblyCommandResult> CreatePoll(Guid eventId, long version, PollTemplate template, string? text, int? threshold) {
        var actor = AuthorizeChair(eventId);
        return await _sessions.CreatePollAsync(eventId, version, new PollTemplateRequest(template, text, threshold), actor, Context.ConnectionAborted);
    }

    public async Task<AssemblyCommandResult> CreateProceduralTopics(Guid eventId) {
        var actor = AuthorizeChair(eventId);
        return await _sessions.CreateProceduralTopicsAsync(eventId, actor, Context.ConnectionAborted);
    }

    public Task<AssemblyCommandResult> ResetPoll(Guid eventId, long version, int pollId) {
        return Command(eventId, (state, now, actor) => state.ResetPoll(version, pollId, actor, now));
    }

    public Task<AssemblyCommandResult> DeletePoll(Guid eventId, long version, int pollId) {
        return Command(eventId, (state, now, actor) => state.DeletePoll(version, pollId, actor, now));
    }

    public Task<AssemblyCommandResult> SetCandidates(Guid eventId, long version, string[] names) {
        return Command(eventId, (state, now, actor) => state.SetCandidates(version, names, actor, now));
    }

    public Task<AssemblyCommandResult> SetCurrentCandidate(Guid eventId, long version, int? index) {
        return Command(eventId, (state, now, actor) => state.SetCurrentCandidate(version, index, actor, now));
    }

    public Task<AssemblyCommandResult> ExtendPoll(Guid eventId, long version, int seconds) {
        return Command(eventId, (state, now, actor) => state.ExtendPoll(version, seconds, actor, now));
    }

    public Task<AssemblyCommandResult> StopPoll(Guid eventId, long version, int pollId) {
        return Command(eventId, (state, now, actor) => state.StopPoll(version, pollId, actor, now));
    }

    public Task<AssemblyCommandResult> PublishPoll(Guid eventId, long version, int pollId) {
        return Command(eventId, (state, now, actor) => state.PublishPoll(version, pollId, actor, now));
    }

    public Task<AssemblyCommandResult> SelectAgendaItem(Guid eventId, long version, int agendaItemId) {
        return Command(eventId, (state, now, actor) => state.SelectAgendaItem(version, agendaItemId, actor, now));
    }

    public Task<AssemblyCommandResult> SetPhase(Guid eventId, long version, AssemblyPhase phase) {
        return Command(eventId, (state, now, actor) => state.SetPhase(version, phase, actor, now));
    }

    public Task<AssemblyCommandResult> StartNextRound(Guid eventId, long version) {
        return Command(eventId, (state, now, actor) => state.StartNextRound(version, actor, now));
    }

    public Task<AssemblyCommandResult> RecordGoAccepted(Guid eventId, long version) {
        return Command(eventId, (state, now, actor) => state.RecordGoAccepted(version, actor, now));
    }

    public Task<AssemblyCommandResult> StartGoObjection(Guid eventId, long version) {
        return Command(eventId, (state, now, actor) => state.StartGoObjection(version, actor, now));
    }

    public Task<AssemblyCommandResult> ResumeAfterGo(Guid eventId, long version) {
        return Command(eventId, (state, now, actor) => state.ResumeAfterGo(version, actor, now));
    }

    public Task<AssemblyCommandResult> SetElectionRunning(Guid eventId, long version, bool running) {
        return Command(eventId, (state, now, actor) => state.SetElectionRunning(version, running, actor, now));
    }

    public Task<AssemblyCommandResult> SetRepeatPresentation(Guid eventId, long version, bool repeat) {
        return Command(eventId, (state, now, actor) => state.SetRepeatPresentation(version, repeat, actor, now));
    }

    private Task<AssemblyCommandResult> Command(Guid eventId,
        Func<AssemblySessionState, DateTime, Guid, (AssemblyCommandResult Result, AssemblyEffects Effects)> command) {
        var actor = AuthorizeChair(eventId);
        return Task.FromResult(_sessions.Execute(eventId, (state, now) => command(state, now, actor)));
    }

    private Guid AuthorizeChair(Guid eventId) {
        _perms.Bind(Context.User!);
        var userId = _perms.UserId;
        if (userId == null)
            throw new HubException("Unauthorized");

        var evt = _events.Get(eventId);
        if (evt == null || !_perms.Has(evt.ChapterId, PermissionIdentifier.AssemblyChair))
            throw new HubException("Forbidden");

        return userId.Value;
    }
}
