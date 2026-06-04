using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;
using Quartermaster.Data.AuditLog;

namespace Quartermaster.Data.ChapterMails;

public class ChapterMailRepository {
    private const string AuditEntityType = "ChapterMail";

    private readonly DbContext _context;
    private readonly AuditLogRepository _auditLog;

    public ChapterMailRepository(DbContext context, AuditLogRepository auditLog) {
        _context = context;
        _auditLog = auditLog;
    }

    public ChapterMail? Get(Guid id)
        => _context.ChapterMails.Where(m => m.Id == id).FirstOrDefault();

    public List<ChapterMail> ListForChapters(List<Guid> chapterIds, bool includeArchived) {
        if (chapterIds.Count == 0)
            return new List<ChapterMail>();

        var q = _context.ChapterMails.Where(m => chapterIds.Contains(m.ChapterId));
        if (!includeArchived)
            q = q.Where(m => m.DeletedAt == null);
        return q.OrderByDescending(m => m.CreatedAt).ToList();
    }

    public void Create(ChapterMail mail) {
        _context.Insert(mail);
        _auditLog.LogCreated(AuditEntityType, mail.Id);
    }

    public void Update(ChapterMail mail, ChapterMail original) {
        _context.ChapterMails
            .Where(m => m.Id == mail.Id)
            .Set(m => m.Title, mail.Title)
            .Set(m => m.Subject, mail.Subject)
            .Set(m => m.TargetType, mail.TargetType)
            .Set(m => m.TargetId, mail.TargetId)
            .Set(m => m.IncludeDescendants, mail.IncludeDescendants)
            .Set(m => m.TemplateId, mail.TemplateId)
            .Set(m => m.Content, mail.Content)
            .Update();

        _auditLog.LogFieldChange(AuditEntityType, mail.Id, "Title", original.Title, mail.Title);
        _auditLog.LogFieldChange(AuditEntityType, mail.Id, "Subject", original.Subject, mail.Subject);
        _auditLog.LogFieldChange(AuditEntityType, mail.Id, "TargetType", original.TargetType.ToString(), mail.TargetType.ToString());
        _auditLog.LogFieldChange(AuditEntityType, mail.Id, "TargetId", original.TargetId.ToString(), mail.TargetId.ToString());
        _auditLog.LogFieldChange(AuditEntityType, mail.Id, "IncludeDescendants", original.IncludeDescendants.ToString(), mail.IncludeDescendants.ToString());
        _auditLog.LogFieldChange(AuditEntityType, mail.Id, "TemplateId", original.TemplateId?.ToString(), mail.TemplateId?.ToString());
        _auditLog.LogFieldChange(AuditEntityType, mail.Id, "Content", original.Content, mail.Content);
    }

    /// <summary>Hard-deletes a draft (unsent) mail; archives (soft-deletes) one that's already been sent so the record survives for audit.</summary>
    public void Delete(ChapterMail mail) {
        if (mail.IsSent) {
            var now = DateTime.UtcNow;
            _context.ChapterMails.Where(m => m.Id == mail.Id).Set(m => m.DeletedAt, (DateTime?)now).Update();
            _auditLog.LogSoftDeleted(AuditEntityType, mail.Id);
        } else {
            _context.ChapterMails.Where(m => m.Id == mail.Id).Delete();
            _auditLog.LogDeleted(AuditEntityType, mail.Id);
        }
    }

    public void MarkSent(Guid id, Guid sentByUserId, int recipientCount, DateTime sentAt) {
        _context.ChapterMails.Where(m => m.Id == id)
            .Set(m => m.SentAt, (DateTime?)sentAt)
            .Set(m => m.SentByUserId, (Guid?)sentByUserId)
            .Set(m => m.RecipientCount, (int?)recipientCount)
            .Update();
        _auditLog.Log(AuditEntityType, id, "Sent", fieldName: "RecipientCount", oldValue: null, newValue: recipientCount.ToString());
    }
}
