using System;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Components.AssemblyAssistant;

/// <summary>Formatting shared by the console and the overlay.</summary>
public static class AssemblyDisplay {
    public static TimeSpan? Remaining(AssemblyTimerDTO timer, DateTime serverNow) {
        if (timer.EndsAt != null)
            return timer.EndsAt.Value - serverNow;
        if (timer.RemainingSeconds != null)
            return TimeSpan.FromSeconds(timer.RemainingSeconds.Value);

        return null;
    }

    /// <summary>"2:05" while time remains, "+0:12" for an overrun.</summary>
    public static string FormatRemaining(TimeSpan remaining) {
        var overrun = remaining < TimeSpan.Zero;
        var seconds = (int)Math.Ceiling(Math.Abs(remaining.TotalSeconds));
        if (overrun)
            seconds = (int)Math.Floor(Math.Abs(remaining.TotalSeconds));

        var text = $"{seconds / 60}:{seconds % 60:00}";
        return overrun ? "+" + text : text;
    }

    public static string FormatDuration(TimeSpan duration) {
        var seconds = Math.Max(0, (int)duration.TotalSeconds);
        return seconds >= 3600 ? $"{seconds / 3600}:{seconds / 60 % 60:00}:{seconds % 60:00}" : $"{seconds / 60}:{seconds % 60:00}";
    }

    /// <summary>Bootstrap text colour: normal, orange from 30 seconds, red at the end.</summary>
    public static string TimerClass(TimeSpan? remaining) {
        if (remaining == null)
            return "text-body";
        if (remaining.Value <= TimeSpan.Zero)
            return "text-danger";

        return remaining.Value.TotalSeconds <= 30 ? "text-warning" : "text-body";
    }

    public static string RoleKey(SpeakerRole role) {
        return AssemblyLabels.RoleKey(role);
    }

    public static string QueueKey(QueueKind kind) {
        return AssemblyLabels.QueueKey(kind);
    }

    public static string PhaseKey(AssemblyPhase phase) {
        return AssemblyLabels.PhaseKey(phase);
    }

    public static string MumbleStateKey(string state) {
        return state switch {
            "Synced" => I18nKey.Ui.AssemblyConsole.StateSynced,
            "Connecting" => I18nKey.Ui.AssemblyConsole.StateConnecting,
            "Reconnecting" => I18nKey.Ui.AssemblyConsole.StateReconnecting,
            _ => I18nKey.Ui.AssemblyConsole.StateDisconnected
        };
    }
}
