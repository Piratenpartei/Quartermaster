using System;

namespace Quartermaster.Api.Events;

public class ChecklistItemCheckRequest {
    public Guid EventId { get; set; }
    public Guid ItemId { get; set; }
    public bool ExecuteAction { get; set; }
}
