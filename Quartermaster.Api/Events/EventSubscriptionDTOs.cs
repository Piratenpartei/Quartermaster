using System;

namespace Quartermaster.Api.Events;

public class EventSubscriptionStateDTO {
    public bool IsSubscribed { get; set; }
    public bool CanSubscribe { get; set; }
}

public class EventSubscriptionListItemDTO {
    public Guid EventId { get; set; }

    /// <summary>False when the viewer can no longer see the event; name and date are then left empty.</summary>
    public bool IsVisible { get; set; }
    public string PublicName { get; set; } = "";
    public DateOnly? EventDate { get; set; }
    public TimeOnly? StartTime { get; set; }
    public EventStatus Status { get; set; }
}
