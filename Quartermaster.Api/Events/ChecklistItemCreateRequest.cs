using System;

namespace Quartermaster.Api.Events;

public class ChecklistItemCreateRequest {
    public Guid EventId { get; set; }
    public int SortOrder { get; set; }
    public int ItemType { get; set; }
    public string Label { get; set; } = "";
    public string? Configuration { get; set; }
}
