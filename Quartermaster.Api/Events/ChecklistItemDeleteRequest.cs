using System;

namespace Quartermaster.Api.Events;

public class ChecklistItemDeleteRequest {
    public Guid EventId { get; set; }
    public Guid ItemId { get; set; }
}
