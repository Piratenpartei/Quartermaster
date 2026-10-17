using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Server.AssemblyAssistant.OpenSlides;

/// <summary>
/// Drives polls along the voting countdown: sets the session's duration when a poll starts, stops the poll when the countdown
/// runs out and publishes polls it saw finish. Every action is sent once per poll, although the 250 ms tick runs ahead of the
/// autoupdate state. Polls created by hand in OpenSlides are handled the same way.
/// </summary>
public class PollAutomation {
    private readonly Dictionary<int, string> _lastState = new();
    private readonly HashSet<int> _stopSent = [];
    private readonly HashSet<int> _publishSent = [];
    private bool initialized;

    public IReadOnlyList<OpenSlidesEffect> OnView(OpenSlidesMeetingView view, PollSettings settings, DateTime nowUtc, IReadOnlyList<int>? projectorIds = null) {
        var effects = new List<OpenSlidesEffect>();
        foreach (var poll in view.Polls) {
            var previous = _lastState.GetValueOrDefault(poll.Id);
            _lastState[poll.Id] = poll.State;

            var newlyStarted = IsNewlyStarted(poll, previous);
            if (newlyStarted && NeedsDuration(view, settings))
                effects.Add(new SetCountdownEffect(view.PollCountdown!.Id, true, ToUnix(nowUtc) + settings.DurationSeconds));
            if (newlyStarted && settings.Project && projectorIds != null && projectorIds.Count > 0)
                effects.Add(new ProjectPollEffect(poll.Id, projectorIds));
            if (poll.State != "started")
                _stopSent.Remove(poll.Id);
            if (IsNewlyFinished(poll, previous) && settings.AutoPublish && _publishSent.Add(poll.Id))
                effects.Add(new PublishPollEffect(poll.Id));
        }

        initialized = true;
        return effects;
    }

    public IReadOnlyList<OpenSlidesEffect> Tick(OpenSlidesMeetingView view, PollSettings settings, DateTime nowUtc) {
        var countdown = view.PollCountdown;
        if (!settings.AutoStop || countdown == null || !countdown.Running)
            return [];
        if (countdown.CountdownTime > ToUnix(nowUtc))
            return [];

        return view.Polls
            .Where(p => p.State == "started" && _stopSent.Add(p.Id))
            .Select(p => (OpenSlidesEffect)new StopPollEffect(p.Id))
            .ToList();
    }

    public IReadOnlyList<OpenSlidesEffect> Extend(OpenSlidesMeetingView view, int seconds) {
        var countdown = view.PollCountdown;
        if (countdown == null || !countdown.Running)
            return [];

        return [new SetCountdownEffect(countdown.Id, true, countdown.CountdownTime + seconds)];
    }

    public static double ToUnix(DateTime utc) {
        return (utc - DateTime.UnixEpoch).TotalSeconds;
    }

    public static DateTime FromUnix(double seconds) {
        return DateTime.UnixEpoch.AddSeconds(seconds);
    }

    // Polls that were already running when the stream (re)connected keep their countdown and projection.
    private bool IsNewlyStarted(OpenSlidesPollView poll, string? previous) {
        if (!initialized)
            return false;

        return poll.State == "started" && previous != "started";
    }

    // Results that were already finished when the stream (re)connected may have been left unpublished on purpose.
    private bool IsNewlyFinished(OpenSlidesPollView poll, string? previous) {
        if (!initialized)
            return false;

        return poll.State == "finished" && previous != null && previous != "finished";
    }

    private static bool NeedsDuration(OpenSlidesMeetingView view, PollSettings settings) {
        return view.PollCountdown != null && view.PollCountdown.DefaultTime != settings.DurationSeconds;
    }
}
