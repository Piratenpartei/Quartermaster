using System;

namespace Quartermaster.Api.Events;

public class ChecklistItemUpdateRequest {
    public Guid EventId { get; set; }
    public Guid ItemId { get; set; }
    public int SortOrder { get; set; }
    public ChecklistItemType ItemType { get; set; }
    public string Label { get; set; } = "";
    public EventChecklistItemConfigDTO? Configuration { get; set; }
}
