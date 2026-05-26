using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Email;
using Quartermaster.Data.Email;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Email;

public class EmailLogRequest {
    [QueryParam]
    public string? SourceEntityType { get; set; }
    [QueryParam]
    public Guid? SourceEntityId { get; set; }
}

public class EmailLogEndpoint : Endpoint<EmailLogRequest, List<EmailLogDTO>> {
    private readonly EmailLogRepository _emailLogRepo;
    private readonly PermissionContext _perms;

    public EmailLogEndpoint(EmailLogRepository emailLogRepo, PermissionContext perms) {
        _emailLogRepo = emailLogRepo;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/emaillogs");
    }

    public override async Task HandleAsync(EmailLogRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.ViewEmailLogs)) {
            await SendForbiddenAsync(ct);
            return;
        }

        List<EmailLog> logs;
        if (!string.IsNullOrEmpty(req.SourceEntityType) && req.SourceEntityId.HasValue) {
            logs = _emailLogRepo.GetForSource(req.SourceEntityType, req.SourceEntityId.Value);
        } else {
            logs = _emailLogRepo.GetRecent();
        }

        var dtos = logs.Select(l => new EmailLogDTO {
            Id = l.Id,
            Recipient = l.Recipient,
            Subject = l.Subject,
            TemplateIdentifier = l.TemplateIdentifier,
            SourceEntityType = l.SourceEntityType,
            SourceEntityId = l.SourceEntityId,
            Status = l.Status,
            Error = l.Error,
            AttemptCount = l.AttemptCount,
            CreatedAt = l.CreatedAt,
            SentAt = l.SentAt
        }).ToList();

        await SendAsync(dtos, cancellation: ct);
    }
}
