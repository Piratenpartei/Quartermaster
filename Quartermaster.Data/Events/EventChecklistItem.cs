using System;
using LinqToDB.Mapping;
using Quartermaster.Api;
using Quartermaster.Api.Events;

namespace Quartermaster.Data.Events;

[Table(TableName, IsColumnAttributeRequired = false)]
public class EventChecklistItem {
    public const string TableName = "EventChecklistItems";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public int SortOrder { get; set; }
    public ChecklistItemType ItemType { get; set; }
    public string Label { get; set; } = "";
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Configuration { get; set; }
    public Guid? ResultId { get; set; }

    public EventChecklistItemDTO ToDto(EventChecklistItemConfigDTO? config) => new() {
        Id = Id,
        SortOrder = SortOrder,
        ItemType = ItemType,
        Label = Label,
        IsCompleted = IsCompleted,
        CompletedAt = CompletedAt.ToDtoUtc(),
        Configuration = config,
        ResultId = ResultId
    };
}
