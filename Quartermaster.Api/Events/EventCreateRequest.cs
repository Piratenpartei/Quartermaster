using System;

namespace Quartermaster.Api.Events;

public class EventCreateRequest {
    public Guid ChapterId { get; set; }
    public string InternalName { get; set; } = "";
    public string PublicName { get; set; } = "";
    public string? Description { get; set; }
    public DateOnly? EventDate { get; set; }

    /// <summary>Berlin wall-clock time on <see cref="EventDate"/>.</summary>
    public TimeOnly? StartTime { get; set; }
    public string? MeetingUrl { get; set; }
    public EventVisibility Visibility { get; set; } = EventVisibility.Private;
}
