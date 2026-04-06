using System;
using LinqToDB.Mapping;
using Quartermaster.Api.Meetings;

namespace Quartermaster.Data.Meetings;

[Table(TableName, IsColumnAttributeRequired = false)]
public class AgendaItem {
    public const string TableName = "AgendaItems";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MeetingId { get; set; }
    public Guid? ParentId { get; set; }
    public int SortOrder { get; set; }
    public string Title { get; set; } = "";
    public AgendaItemType ItemType { get; set; }
    public Guid? MotionId { get; set; }
    public string? Notes { get; set; }
    public string? Resolution { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
