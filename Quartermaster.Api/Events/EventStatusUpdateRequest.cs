using System;

namespace Quartermaster.Api.Events;

public class EventStatusUpdateRequest {
    public Guid Id { get; set; }
    public EventStatus Status { get; set; }
}
