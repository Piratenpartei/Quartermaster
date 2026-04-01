using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;

namespace Quartermaster.Data.Email;

public class EmailLogRepository {
    private readonly DbContext _context;

    public EmailLogRepository(DbContext context) {
        _context = context;
    }

    public void Create(EmailLog log) => _context.Insert(log);

    public void UpdateStatus(Guid id, string status, string? error, DateTime? sentAt) {
        _context.EmailLogs
            .Where(l => l.Id == id)
            .Set(l => l.Status, status)
            .Set(l => l.Error, error)
            .Set(l => l.SentAt, sentAt)
            .Update();
    }

    public void IncrementAttempt(Guid id) {
        _context.EmailLogs
            .Where(l => l.Id == id)
            .Set(l => l.AttemptCount, l => l.AttemptCount + 1)
            .Update();
    }

    public List<EmailLog> GetForSource(string entityType, Guid entityId) {
        return _context.EmailLogs
            .Where(l => l.SourceEntityType == entityType && l.SourceEntityId == entityId)
            .OrderByDescending(l => l.CreatedAt)
            .ToList();
    }

    public List<EmailLog> GetPending() {
        return _context.EmailLogs
            .Where(l => l.Status == "Pending")
            .OrderBy(l => l.CreatedAt)
            .ToList();
    }

    public List<EmailLog> GetRecent(int count = 50) {
        return _context.EmailLogs
            .OrderByDescending(l => l.CreatedAt)
            .Take(count)
            .ToList();
    }

    public EmailLog? GetById(Guid id)
        => _context.EmailLogs.Where(l => l.Id == id).FirstOrDefault();
}
