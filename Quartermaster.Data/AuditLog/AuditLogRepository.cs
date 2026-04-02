using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;

namespace Quartermaster.Data.AuditLog;

public class AuditLogRepository {
    private readonly DbContext _context;
    private Guid? _currentUserId;
    private string _currentUserDisplayName = "System";

    public AuditLogRepository(DbContext context) {
        _context = context;
    }

    public void SetCurrentUser(Guid? userId, string displayName) {
        _currentUserId = userId;
        _currentUserDisplayName = displayName;
    }

    public void Log(string entityType, Guid entityId, string action, string? fieldName = null, string? oldValue = null, string? newValue = null) {
        _context.Insert(new AuditLog {
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue,
            UserId = _currentUserId,
            UserDisplayName = _currentUserDisplayName,
            Timestamp = DateTime.UtcNow
        });
    }

    public void LogCreated(string entityType, Guid entityId) {
        Log(entityType, entityId, "Created");
    }

    public void LogFieldChange(string entityType, Guid entityId, string fieldName, string? oldValue, string? newValue) {
        if (oldValue == newValue)
            return;
        Log(entityType, entityId, "Updated", fieldName, oldValue, newValue);
    }

    public void LogDeleted(string entityType, Guid entityId) {
        Log(entityType, entityId, "Deleted");
    }

    public void LogSoftDeleted(string entityType, Guid entityId) {
        Log(entityType, entityId, "SoftDeleted");
    }

    public List<AuditLog> GetForEntity(string entityType, Guid entityId) {
        return _context.AuditLogs
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderByDescending(a => a.Timestamp)
            .ToList();
    }
}
