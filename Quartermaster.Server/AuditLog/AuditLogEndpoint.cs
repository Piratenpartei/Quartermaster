using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.AuditLog;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.Events;
using Quartermaster.Data.Meetings;
using Quartermaster.Data.Motions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.AuditLog;

public class AuditLogRequest {
    [QueryParam]
    public string EntityType { get; set; } = "";
    [QueryParam]
    public Guid EntityId { get; set; }
}

/// <summary>
/// Global ViewAudit sees every audit log. Otherwise events, meetings and motions show theirs to whoever may
/// edit them on their chapter; every other entity type, members included, needs ViewAudit.
/// </summary>
public class AuditLogEndpoint : Endpoint<AuditLogRequest, List<AuditLogDTO>> {
    private readonly AuditLogRepository _auditLogRepo;
    private readonly EventRepository _eventRepo;
    private readonly MeetingRepository _meetingRepo;
    private readonly MotionRepository _motionRepo;
    private readonly PermissionContext _perms;

    public AuditLogEndpoint(AuditLogRepository auditLogRepo, EventRepository eventRepo, MeetingRepository meetingRepo,
        MotionRepository motionRepo, PermissionContext perms) {
        _auditLogRepo = auditLogRepo;
        _eventRepo = eventRepo;
        _meetingRepo = meetingRepo;
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

        var chapterId = EditableChapterId(req.EntityType, req.EntityId);
        if (chapterId == null)
            return false;
        return _perms.Has(chapterId.Value, EditPermissionFor(req.EntityType));
    }

    private Guid? EditableChapterId(string entityType, Guid entityId) {
        return entityType switch {
            "Event" => _eventRepo.Get(entityId)?.ChapterId,
            "Meeting" => _meetingRepo.Get(entityId)?.ChapterId,
            "Motion" => _motionRepo.Get(entityId)?.ChapterId,
            _ => null
        };
    }

    private static string EditPermissionFor(string entityType) {
        return entityType switch {
            "Event" => PermissionIdentifier.EditEvents,
            "Meeting" => PermissionIdentifier.EditMeetings,
            "Motion" => PermissionIdentifier.EditMotions,
            _ => throw new ArgumentOutOfRangeException(nameof(entityType), entityType, null)
        };
    }
}
