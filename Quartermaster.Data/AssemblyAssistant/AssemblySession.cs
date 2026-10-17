using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.AssemblyAssistant;

[Table(TableName, IsColumnAttributeRequired = false)]
public class AssemblySession {
    public const string TableName = "AssemblySessions";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EventId { get; set; }
    public int? OpenSlidesMeetingId { get; set; }
    public DateTime StartedAt { get; set; }
    public Guid StartedByUserId { get; set; }
    public DateTime? EndedAt { get; set; }
    public DateTime? LogDeletedAt { get; set; }
}
