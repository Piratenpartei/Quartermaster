using System;
using LinqToDB.Mapping;
using Quartermaster.Api.AssemblyAssistant;

namespace Quartermaster.Data.AssemblyAssistant;

[Table(TableName, IsColumnAttributeRequired = false)]
public class AssemblyLogEntry {
    public const string TableName = "AssemblyLogEntries";
    public const int ItemTitleMaxLength = 500;
    public const int MumbleNameMaxLength = 200;

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SessionId { get; set; }
    public DateTime At { get; set; }
    public AssemblyLogKind Kind { get; set; }
    public string? ItemTitle { get; set; }
    public int? OpenSlidesAgendaItemId { get; set; }
    public string? MumbleName { get; set; }
    public string? Role { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int? AllottedSeconds { get; set; }
    public int? OpenSlidesPollId { get; set; }
    public string? Detail { get; set; }
    public Guid? ActorUserId { get; set; }
}
