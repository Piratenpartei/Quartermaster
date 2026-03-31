using System;

namespace Quartermaster.Api.Events;

public class EventChecklistItemDTO {
    public Guid Id { get; set; }
    public int SortOrder { get; set; }
    public int ItemType { get; set; }
    public string Label { get; set; } = "";
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Configuration { get; set; }
    public Guid? ResultId { get; set; }
}
