using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Email;
using Quartermaster.Data.Email;

namespace Quartermaster.Server.Email;

public class EmailLogRequest {
    [QueryParam]
    public string? SourceEntityType { get; set; }
    [QueryParam]
    public Guid? SourceEntityId { get; set; }
}

public class EmailLogEndpoint : Endpoint<EmailLogRequest, List<EmailLogDTO>> {
    private readonly EmailLogRepository _emailLogRepo;

    public EmailLogEndpoint(EmailLogRepository emailLogRepo) {
        _emailLogRepo = emailLogRepo;
    }

    public override void Configure() {
        Get("/api/emaillogs");
        AllowAnonymous();
    }

    public override async Task HandleAsync(EmailLogRequest req, CancellationToken ct) {
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
