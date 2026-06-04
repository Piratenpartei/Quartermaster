using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Notifications;
using Quartermaster.Data.Notifications;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Notifications;

public class NotificationStatsRequest {
    [QueryParam]
    public DateTime? From { get; set; }
    [QueryParam]
    public DateTime? To { get; set; }
    [QueryParam]
    public string? ChannelId { get; set; }
    [QueryParam]
    public string? TriggerId { get; set; }
    [QueryParam]
    public string? TemplateIdentifier { get; set; }
    [QueryParam]
    public string? SourceEntityType { get; set; }
    [QueryParam]
    public NotificationStatsGroupByDTO GroupBy { get; set; } = NotificationStatsGroupByDTO.None;
}

public class NotificationStatsEndpoint : Endpoint<NotificationStatsRequest, NotificationStatsDTO> {
    private readonly NotificationLogRepository _logRepo;
    private readonly PermissionContext _perms;

    public NotificationStatsEndpoint(NotificationLogRepository logRepo, PermissionContext perms) {
        _logRepo = logRepo;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/notifications/stats");
    }

    public override async Task HandleAsync(NotificationStatsRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.ViewNotificationLogs)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var filter = new NotificationStatsFilter {
            From = req.From,
            To = req.To,
            ChannelId = req.ChannelId,
            TriggerId = req.TriggerId,
            TemplateIdentifier = req.TemplateIdentifier,
            SourceEntityType = req.SourceEntityType,
            GroupBy = (NotificationStatsGroupBy)(int)req.GroupBy
        };
        var aggregate = _logRepo.AggregateStats(filter);

        var dto = new NotificationStatsDTO {
            Sent = aggregate.Sent,
            Failed = aggregate.Failed,
            MdnRequested = aggregate.MdnRequested,
            MdnReceived = aggregate.MdnReceived,
            Bounced = aggregate.Bounced,
            AverageTimeToReadSeconds = aggregate.AverageTimeToReadSeconds,
            Buckets = aggregate.Buckets
                .Select(b => new NotificationStatsBucketDTO {
                    Key = b.Key,
                    Sent = b.Sent,
                    MdnRequested = b.MdnRequested,
                    MdnReceived = b.MdnReceived,
                    Bounced = b.Bounced
                })
                .ToList()
        };

        await SendAsync(dto, cancellation: ct);
    }
}
