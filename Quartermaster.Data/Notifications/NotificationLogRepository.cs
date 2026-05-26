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
}
