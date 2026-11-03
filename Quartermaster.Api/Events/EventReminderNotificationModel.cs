namespace Quartermaster.Api.Events;

/// <summary>Template model <c>event</c> of the event reminder notifications; dates and times are Berlin wall-clock text.</summary>
public record EventReminderNotificationModel {
    public const string KindDayBefore = "day_before";
    public const string KindToday = "today";
    public const string KindSoon = "soon";
    public const string KindChanged = "changed";

    public string Name { get; set; } = "";
    public string NameEscaped { get; set; } = "";
    public string Date { get; set; } = "";
    public string Time { get; set; } = "";

    /// <summary><see cref="KindDayBefore"/>, <see cref="KindToday"/>, <see cref="KindSoon"/> or <see cref="KindChanged"/>; empty for deadline reminders.</summary>
    public string Kind { get; set; } = "";

    /// <summary>The motion types whose deadline it is, Markdown-escaped; only for deadline reminders.</summary>
    public string TypeNames { get; set; } = "";

    /// <summary>Only set for <see cref="KindSoon"/>.</summary>
    public string MeetingUrl { get; set; } = "";
    public string Path { get; set; } = "";
}
