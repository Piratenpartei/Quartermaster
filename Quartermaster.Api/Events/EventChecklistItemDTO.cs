using System;

namespace Quartermaster.Api.Events;

public class EventChecklistItemDTO {
    public Guid Id { get; set; }
    public int SortOrder { get; set; }
    public ChecklistItemType ItemType { get; set; }
    public string Label { get; set; } = "";
    public bool IsCompleted { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public EventChecklistItemConfigDTO? Configuration { get; set; }
    public Guid? ResultId { get; set; }
}
