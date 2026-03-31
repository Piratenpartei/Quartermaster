using System;

namespace Quartermaster.Api.Events;

public class ChecklistItemUncheckRequest {
    public Guid EventId { get; set; }
    public Guid ItemId { get; set; }
}
