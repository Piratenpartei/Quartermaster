using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.AuditLog;

[Table(TableName)]
public class AuditLog {
    public const string TableName = "AuditLogs";

    [Column, PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column]
    public string EntityType { get; set; } = "";

    [Column]
    public Guid EntityId { get; set; }

    [Column]
    public string Action { get; set; } = "";

    [Column]
    public string? FieldName { get; set; }

    [Column]
    public string? OldValue { get; set; }

    [Column]
    public string? NewValue { get; set; }

    [Column]
    public Guid? UserId { get; set; }

    [Column]
    public string? UserDisplayName { get; set; }

    [Column]
    public DateTime Timestamp { get; set; }
}
