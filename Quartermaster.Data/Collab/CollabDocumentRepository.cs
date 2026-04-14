using System;
using System.Linq;
using LinqToDB;

namespace Quartermaster.Data.Collab;

public class CollabDocumentRepository {
    private readonly DbContext _context;

    public CollabDocumentRepository(DbContext context) {
        _context = context;
    }

    public CollabDocument? Get(string entityType, Guid entityId) {
        return _context.CollabDocuments
            .Where(d => d.EntityType == entityType && d.EntityId == entityId)
            .FirstOrDefault();
    }

    /// <summary>
    /// Insert if missing, update if present. Upsert is keyed on
    /// (EntityType, EntityId). The doc Id is preserved on update.
    /// </summary>
    public void Upsert(CollabDocument doc) {
        var existing = Get(doc.EntityType, doc.EntityId);
        var now = DateTime.UtcNow;

        if (existing == null) {
            if (doc.Id == Guid.Empty)
                doc.Id = Guid.NewGuid();
            doc.CreatedAt = now;
            doc.LastUpdatedAt = now;
            _context.Insert(doc);
            return;
        }

        _context.CollabDocuments.Where(d => d.Id == existing.Id)
            .Set(d => d.DocumentState, doc.DocumentState)
            .Set(d => d.PlainText, doc.PlainText)
            .Set(d => d.ClientUserMap, doc.ClientUserMap)
            .Set(d => d.LastUpdatedAt, now)
            .Set(d => d.LastUpdatedByUserId, doc.LastUpdatedByUserId)
            .Update();
    }
}
