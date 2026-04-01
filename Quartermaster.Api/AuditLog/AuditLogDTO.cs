using System;

namespace Quartermaster.Api.AuditLog;

public class AuditLogDTO {
    public Guid Id { get; set; }
    public string EntityType { get; set; } = "";
    public Guid EntityId { get; set; }
    public string Action { get; set; } = "";
    public string? FieldName { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? UserDisplayName { get; set; }
    public DateTime Timestamp { get; set; }
}
