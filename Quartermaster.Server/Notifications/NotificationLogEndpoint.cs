using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Notifications;
using Quartermaster.Data.Notifications;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Notifications;

public class NotificationLogRequest {
    [QueryParam]
    public string? SourceEntityType { get; set; }
    [QueryParam]
    public Guid? SourceEntityId { get; set; }
}

public class NotificationLogEndpoint : Endpoint<NotificationLogRequest, List<NotificationLogDTO>> {
    private readonly NotificationLogRepository _logRepo;
    private readonly PermissionContext _perms;

    public NotificationLogEndpoint(NotificationLogRepository logRepo, PermissionContext perms) {
        _logRepo = logRepo;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/notificationlogs");
    }

    public override async Task HandleAsync(NotificationLogRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.ViewNotificationLogs)) {
            await SendForbiddenAsync(ct);
            return;
        }

        List<NotificationLog> logs;
        if (!string.IsNullOrEmpty(req.SourceEntityType) && req.SourceEntityId.HasValue) {
            logs = _logRepo.GetForSource(req.SourceEntityType, req.SourceEntityId.Value);
        } else {
            logs = _logRepo.GetRecent();
        }

        var dtos = logs.Select(l => new NotificationLogDTO {
            Id = l.Id,
            ChannelId = l.ChannelId,
            Recipient = l.Recipient,
            RecipientUserId = l.RecipientUserId,
            Subject = l.Subject,
            TriggerId = l.TriggerId,
            TemplateIdentifier = l.TemplateIdentifier,
            SourceEntityType = l.SourceEntityType,
            SourceEntityId = l.SourceEntityId,
            Status = l.Status,
            Error = l.Error,
            AttemptCount = l.AttemptCount,
            CreatedAt = l.CreatedAt.ToDtoUtc(),
            SentAt = l.SentAt.ToDtoUtc()
        }).ToList();

        await SendAsync(dtos, cancellation: ct);
    }
}
