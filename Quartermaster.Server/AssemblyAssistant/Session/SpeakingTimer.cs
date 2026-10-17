using System;
using Quartermaster.Api.AssemblyAssistant;

namespace Quartermaster.Server.AssemblyAssistant.Session;

/// <summary>
/// Per-speech countdown. It starts with the speaker's first voice packet (or by hand); a null allotted time means the
/// contribution is untimed (GO motions, answers of the motion author).
/// </summary>
public class SpeakingTimer {
    public const int WarningSeconds = 30;

    public int? AllottedSeconds { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? PausedAt { get; private set; }
    public TimeSpan PausedTotal { get; private set; }
    public bool WarningSent { get; set; }
    public bool ExpiredSent { get; set; }

    public bool IsStarted => StartedAt != null;
    public bool IsPaused => PausedAt != null;

    public SpeakingTimer(int? allottedSeconds) {
        AllottedSeconds = allottedSeconds;
    }

    public static SpeakingTimer Restored(int? allottedSeconds, DateTime startedAt) {
        return new SpeakingTimer(allottedSeconds) { StartedAt = startedAt };
    }

    public void Start(DateTime nowUtc) {
        StartedAt ??= nowUtc;
    }

    public void Pause(DateTime nowUtc) {
        if (IsStarted && !IsPaused)
            PausedAt = nowUtc;
    }

    public void Resume(DateTime nowUtc) {
        if (PausedAt == null)
            return;

        PausedTotal += nowUtc - PausedAt.Value;
        PausedAt = null;
    }

    public void AddSeconds(int seconds, DateTime nowUtc) {
        if (AllottedSeconds == null)
            return;

        AllottedSeconds += seconds;
        ResetWarnings(nowUtc);
    }

    public void SetAllotted(int seconds, DateTime nowUtc) {
        AllottedSeconds = seconds;
        ResetWarnings(nowUtc);
    }

    public TimeSpan? Remaining(DateTime nowUtc) {
        if (AllottedSeconds == null)
            return null;
        if (StartedAt == null)
            return TimeSpan.FromSeconds(AllottedSeconds.Value);

        var elapsed = (PausedAt ?? nowUtc) - StartedAt.Value - PausedTotal;
        return TimeSpan.FromSeconds(AllottedSeconds.Value) - elapsed;
    }

    public DateTime? EndsAt() {
        if (AllottedSeconds == null || StartedAt == null || IsPaused)
            return null;

        return StartedAt.Value + PausedTotal + TimeSpan.FromSeconds(AllottedSeconds.Value);
    }

    public AssemblyTimerDTO ToDto(DateTime nowUtc, bool offerStartNow) {
        var endsAt = EndsAt();
        var remaining = Remaining(nowUtc);
        return new AssemblyTimerDTO {
            AllottedSeconds = AllottedSeconds,
            StartedAt = StartedAt,
            EndsAt = endsAt,
            RemainingSeconds = endsAt == null && remaining != null ? (int)Math.Ceiling(remaining.Value.TotalSeconds) : null,
            Paused = IsPaused,
            OfferStartNow = offerStartNow
        };
    }

    // An extension can bring the speaker back above the warning or expiry point; the texts must then be sent again.
    private void ResetWarnings(DateTime nowUtc) {
        var remaining = Remaining(nowUtc);
        if (remaining == null)
            return;

        if (remaining.Value.TotalSeconds > WarningSeconds)
            WarningSent = false;
        if (remaining.Value.TotalSeconds > 0)
            ExpiredSent = false;
    }
}
