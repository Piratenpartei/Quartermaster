using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.AuditLog;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.Motions;
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
    private readonly MotionRepository _motionRepo;
    private readonly PermissionContext _perms;

    public AuditLogEndpoint(AuditLogRepository auditLogRepo, MotionRepository motionRepo, PermissionContext perms) {
        _auditLogRepo = auditLogRepo;
        _motionRepo = motionRepo;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/auditlog");
    }

    public override async Task HandleAsync(AuditLogRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!IsAuthorizedFor(req)) {
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
            Timestamp = l.Timestamp.ToDtoUtc()
        }).ToList();
        await SendAsync(dtos, cancellation: ct);
    }

    private bool IsAuthorizedFor(AuditLogRequest req) {
        if (_perms.HasGlobal(PermissionIdentifier.ViewAudit))
            return true;
        if (req.EntityType == "Motion") {
            var motion = _motionRepo.Get(req.EntityId);
            if (motion != null && _perms.Has(motion.ChapterId, PermissionIdentifier.ViewMotions))
                return true;
        }
        return false;
    }
}
