using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.AuditLog;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.AuditLog;

public class AuditLogRequest {
    [QueryParam]
    public string EntityType { get; set; } = "";
    [QueryParam]
    public Guid EntityId { get; set; }
}

public class AuditLogEndpoint : Endpoint<AuditLogRequest, List<AuditLogDTO>> {
    private readonly AuditLogRepository _auditLogRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;

    public AuditLogEndpoint(AuditLogRepository auditLogRepo, UserGlobalPermissionRepository globalPermRepo) {
        _auditLogRepo = auditLogRepo;
        _globalPermRepo = globalPermRepo;
    }

    public override void Configure() {
        Get("/api/auditlog");
    }

    public override async Task HandleAsync(AuditLogRequest req, CancellationToken ct) {
        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!EndpointAuthorizationHelper.HasGlobalPermission(userId.Value, PermissionIdentifier.ViewAudit, _globalPermRepo)) {
            await SendForbiddenAsync(ct);
            return;
        }

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
