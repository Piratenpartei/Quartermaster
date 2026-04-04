using System;

namespace Quartermaster.Api.Events;

public class EventUpdateRequest {
    public Guid Id { get; set; }
    public string InternalName { get; set; } = "";
    public string PublicName { get; set; } = "";
    public string? Description { get; set; }
    public DateTime? EventDate { get; set; }
    public EventVisibility Visibility { get; set; } = EventVisibility.Private;
}
