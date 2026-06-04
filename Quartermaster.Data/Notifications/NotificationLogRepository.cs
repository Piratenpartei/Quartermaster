using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;

namespace Quartermaster.Data.Notifications;

public class NotificationLogRepository {
    private readonly DbContext _context;

    public NotificationLogRepository(DbContext context) {
        _context = context;
    }

    public void Create(NotificationLog log) => _context.Insert(log);

    public void UpdateStatus(Guid id, string status, string? error, DateTime? sentAt) {
        _context.NotificationLogs
            .Where(l => l.Id == id)
            .Set(l => l.Status, status)
            .Set(l => l.Error, error)
            .Set(l => l.SentAt, sentAt)
            .Update();
    }

    public void IncrementAttempt(Guid id) {
        _context.NotificationLogs
            .Where(l => l.Id == id)
            .Set(l => l.AttemptCount, l => l.AttemptCount + 1)
            .Update();
    }

    public List<NotificationLog> GetForSource(string entityType, Guid entityId) {
        return _context.NotificationLogs
            .Where(l => l.SourceEntityType == entityType && l.SourceEntityId == entityId)
            .OrderByDescending(l => l.CreatedAt)
            .ToList();
    }

    /// <summary>
    /// Pending rows for the given channel. The Email background service uses this on startup
    /// to re-enqueue work that was in-flight when the server crashed.
    /// </summary>
    public List<NotificationLog> GetPendingForChannel(string channelId) {
        return _context.NotificationLogs
            .Where(l => l.Status == "Pending" && l.ChannelId == channelId)
            .OrderBy(l => l.CreatedAt)
            .ToList();
    }

    public List<NotificationLog> GetRecent(int count = 50) {
        return _context.NotificationLogs
            .OrderByDescending(l => l.CreatedAt)
            .Take(count)
            .ToList();
    }

    public NotificationLog? Get(Guid id)
        => _context.NotificationLogs.Where(l => l.Id == id).FirstOrDefault();

    public void RecordMdnRequest(Guid id, string messageId, DateTime requestedAt) {
        _context.NotificationLogs
            .Where(l => l.Id == id)
            .Set(l => l.MessageId, messageId)
            .Set(l => l.MdnRequestedAt, (DateTime?)requestedAt)
            .Update();
    }

    public void RecordOutboundMessageId(Guid id, string messageId) {
        _context.NotificationLogs
            .Where(l => l.Id == id)
            .Set(l => l.MessageId, messageId)
            .Update();
    }

    /// <summary>Finds the log row a received MDN belongs to by matching the captured outbound Message-Id; null when the MDN doesn't correspond to anything we sent.</summary>
    public NotificationLog? FindByMessageId(string messageId)
        => _context.NotificationLogs
            .Where(l => l.MessageId == messageId)
            .FirstOrDefault();

    public void RecordMdnReceived(Guid id, string disposition, DateTime receivedAt) {
        _context.NotificationLogs
            .Where(l => l.Id == id)
            .Set(l => l.MdnReceivedAt, (DateTime?)receivedAt)
            .Set(l => l.MdnDisposition, disposition)
            .Update();
    }

    public void RecordBounce(Guid id, string diagnostic, DateTime bouncedAt) {
        _context.NotificationLogs
            .Where(l => l.Id == id)
            .Set(l => l.Status, "Bounced")
            .Set(l => l.BouncedAt, (DateTime?)bouncedAt)
            .Set(l => l.BounceDiagnostic, diagnostic)
            .Update();
    }

    public List<string> GetDistinctSourceEntityTypes() {
        return _context.NotificationLogs
            .Where(l => l.SourceEntityType != null && l.SourceEntityType != "")
            .Select(l => l.SourceEntityType!)
            .Distinct()
            .OrderBy(s => s)
            .ToList();
    }

    public NotificationStatsAggregate AggregateStats(NotificationStatsFilter filter) {
        var q = _context.NotificationLogs.AsQueryable();
        if (filter.From.HasValue) {
            q = q.Where(l => l.CreatedAt >= filter.From.Value);
        }
        if (filter.To.HasValue) {
            q = q.Where(l => l.CreatedAt < filter.To.Value);
        }
        if (!string.IsNullOrEmpty(filter.ChannelId)) {
            q = q.Where(l => l.ChannelId == filter.ChannelId);
        }
        if (!string.IsNullOrEmpty(filter.TriggerId)) {
            q = q.Where(l => l.TriggerId == filter.TriggerId);
        }
        if (!string.IsNullOrEmpty(filter.TemplateIdentifier)) {
            q = q.Where(l => l.TemplateIdentifier == filter.TemplateIdentifier);
        }
        if (!string.IsNullOrEmpty(filter.SourceEntityType)) {
            q = q.Where(l => l.SourceEntityType == filter.SourceEntityType);
        }

        var totals = q
            .GroupBy(_ => 1)
            .Select(g => new {
                Sent = g.Count(l => l.Status == "Sent"),
                Failed = g.Count(l => l.Status == "Failed"),
                MdnRequested = g.Count(l => l.MdnRequestedAt != null),
                MdnReceived = g.Count(l => l.MdnReceivedAt != null),
                Bounced = g.Count(l => l.BouncedAt != null),
                AverageTimeToReadSeconds = g
                    .Where(l => l.MdnReceivedAt != null && l.SentAt != null)
                    .Average(l => (double?)Sql.DateDiff(Sql.DateParts.Second, l.SentAt!.Value, l.MdnReceivedAt!.Value)) ?? 0d
            })
            .FirstOrDefault();

        var aggregate = new NotificationStatsAggregate {
            Sent = totals?.Sent ?? 0,
            Failed = totals?.Failed ?? 0,
            MdnRequested = totals?.MdnRequested ?? 0,
            MdnReceived = totals?.MdnReceived ?? 0,
            Bounced = totals?.Bounced ?? 0,
            AverageTimeToReadSeconds = totals?.AverageTimeToReadSeconds ?? 0d
        };

        if (filter.GroupBy == NotificationStatsGroupBy.None) {
            return aggregate;
        }

        var groupedRaw = filter.GroupBy switch {
            NotificationStatsGroupBy.Trigger => q
                .GroupBy(l => l.TriggerId ?? "")
                .Select(g => new NotificationStatsBucket {
                    Key = g.Key,
                    Sent = g.Count(l => l.Status == "Sent"),
                    MdnRequested = g.Count(l => l.MdnRequestedAt != null),
                    MdnReceived = g.Count(l => l.MdnReceivedAt != null),
                    Bounced = g.Count(l => l.BouncedAt != null)
                })
                .ToList(),
            NotificationStatsGroupBy.Template => q
                .GroupBy(l => l.TemplateIdentifier ?? "")
                .Select(g => new NotificationStatsBucket {
                    Key = g.Key,
                    Sent = g.Count(l => l.Status == "Sent"),
                    MdnRequested = g.Count(l => l.MdnRequestedAt != null),
                    MdnReceived = g.Count(l => l.MdnReceivedAt != null),
                    Bounced = g.Count(l => l.BouncedAt != null)
                })
                .ToList(),
            NotificationStatsGroupBy.SourceEntityType => q
                .GroupBy(l => l.SourceEntityType ?? "")
                .Select(g => new NotificationStatsBucket {
                    Key = g.Key,
                    Sent = g.Count(l => l.Status == "Sent"),
                    MdnRequested = g.Count(l => l.MdnRequestedAt != null),
                    MdnReceived = g.Count(l => l.MdnReceivedAt != null),
                    Bounced = g.Count(l => l.BouncedAt != null)
                })
                .ToList(),
            NotificationStatsGroupBy.Channel => q
                .GroupBy(l => l.ChannelId)
                .Select(g => new NotificationStatsBucket {
                    Key = g.Key,
                    Sent = g.Count(l => l.Status == "Sent"),
                    MdnRequested = g.Count(l => l.MdnRequestedAt != null),
                    MdnReceived = g.Count(l => l.MdnReceivedAt != null),
                    Bounced = g.Count(l => l.BouncedAt != null)
                })
                .ToList(),
            _ => new List<NotificationStatsBucket>()
        };

        aggregate.Buckets = groupedRaw
            .OrderByDescending(b => b.Sent)
            .ToList();
        return aggregate;
    }
}
