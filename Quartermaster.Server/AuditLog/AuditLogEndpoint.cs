using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.AuditLog;
using Quartermaster.Data.AuditLog;

namespace Quartermaster.Server.AuditLog;

public class AuditLogRequest {
    [QueryParam]
    public string EntityType { get; set; } = "";
    [QueryParam]
    public Guid EntityId { get; set; }
}

public class AuditLogEndpoint : Endpoint<AuditLogRequest, List<AuditLogDTO>> {
    private readonly AuditLogRepository _auditLogRepo;

    public AuditLogEndpoint(AuditLogRepository auditLogRepo) {
        _auditLogRepo = auditLogRepo;
    }

    public override void Configure() {
        Get("/api/auditlog");
        AllowAnonymous();
    }

    public override async Task HandleAsync(AuditLogRequest req, CancellationToken ct) {
        var logs = _auditLogRepo.GetForEntity(req.EntityType, req.EntityId);
        var dtos = logs.Select(l => new AuditLogDTO {
            Id = l.Id,
            EntityType = l.EntityType,
            EntityId = l.EntityId,
            Action = l.Action,
            FieldName = l.FieldName,
            OldValue = l.OldValue,
            NewValue = l.NewValue,
            UserDisplayName = l.UserDisplayName,
            Timestamp = l.Timestamp
        }).ToList();
        await SendAsync(dtos, cancellation: ct);
    }
}
