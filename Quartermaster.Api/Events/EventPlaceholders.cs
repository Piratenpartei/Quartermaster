using System;
using System.Collections.Generic;

namespace Quartermaster.Api.Events;

/// <summary>The placeholders filled from the event itself in descriptions, checklist items and mails, as opposed to template variables.</summary>
public static class EventPlaceholders {
    public static readonly IReadOnlySet<string> BuiltInNames = new HashSet<string> { "date", "datum", "time", "uhrzeit", "link" };

    public static string Apply(string text, DateOnly? date, TimeOnly? startTime, string? meetingUrl, string missingDate = "") {
        var dateText = date.HasValue ? FormatDate(date.Value) : missingDate;
        var timeText = startTime.HasValue ? FormatTime(startTime.Value) : "";
        return text
            .Replace("{{date}}", dateText)
            .Replace("{{datum}}", dateText)
            .Replace("{{time}}", timeText)
            .Replace("{{uhrzeit}}", timeText)
            .Replace("{{link}}", meetingUrl ?? "");
    }

    public static string FormatDate(DateOnly date) {
        return date.ToString("dd.MM.yyyy");
    }

    public static string FormatTime(TimeOnly time) {
        return time.ToString("HH:mm");
    }
}
