using LinqToDB;
using Quartermaster.Data.AuditLog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Data.Events;

public class EventRepository {
    private readonly DbContext _context;
    private readonly AuditLogRepository _auditLog;

    public EventRepository(DbContext context, AuditLogRepository auditLog) {
        _context = context;
        _auditLog = auditLog;
    }

    public Event? Get(Guid id)
        => _context.Events.Where(e => e.Id == id && e.DeletedAt == null).FirstOrDefault();

    public void Create(Event ev) {
        _context.Insert(ev);
        _auditLog.LogCreated("Event", ev.Id);
    }

    public void Update(Event ev) {
        var existing = _context.Events.Where(e => e.Id == ev.Id).FirstOrDefault();

        _context.Events
            .Where(e => e.Id == ev.Id)
            .Set(e => e.InternalName, ev.InternalName)
            .Set(e => e.PublicName, ev.PublicName)
            .Set(e => e.Description, ev.Description)
            .Set(e => e.EventDate, ev.EventDate)
            .Update();

        if (existing != null) {
            _auditLog.LogFieldChange("Event", ev.Id, "InternalName", existing.InternalName, ev.InternalName);
            _auditLog.LogFieldChange("Event", ev.Id, "PublicName", existing.PublicName, ev.PublicName);
            _auditLog.LogFieldChange("Event", ev.Id, "Description", existing.Description, ev.Description);
            _auditLog.LogFieldChange("Event", ev.Id, "EventDate", existing.EventDate?.ToString("o"), ev.EventDate?.ToString("o"));
        }
    }

    public void SetArchived(Guid id, bool archived) {
        var existing = _context.Events.Where(e => e.Id == id).FirstOrDefault();

        _context.Events
            .Where(e => e.Id == id)
            .Set(e => e.IsArchived, archived)
            .Update();

        _auditLog.LogFieldChange("Event", id, "IsArchived", existing?.IsArchived.ToString(), archived.ToString());
    }

    public (List<Event> Items, int TotalCount) Search(Guid? chapterId, bool includeArchived, int page, int pageSize) {
        var q = _context.Events.Where(e => e.DeletedAt == null).AsQueryable();

        if (chapterId.HasValue)
            q = q.Where(e => e.ChapterId == chapterId.Value);

        if (!includeArchived)
            q = q.Where(e => !e.IsArchived);

        var totalCount = q.Count();
        var items = q.OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (items, totalCount);
    }

    public List<EventChecklistItem> GetChecklistItems(Guid eventId)
        => _context.EventChecklistItems
            .Where(i => i.EventId == eventId)
            .OrderBy(i => i.SortOrder)
            .ToList();

    public EventChecklistItem? GetChecklistItem(Guid itemId)
        => _context.EventChecklistItems.Where(i => i.Id == itemId).FirstOrDefault();

    public void CreateChecklistItem(EventChecklistItem item) {
        _context.Insert(item);
        _auditLog.LogCreated("EventChecklistItem", item.Id);
    }

    public void UpdateChecklistItem(EventChecklistItem item) {
        var existing = _context.EventChecklistItems.Where(i => i.Id == item.Id).FirstOrDefault();

        _context.EventChecklistItems
            .Where(i => i.Id == item.Id)
            .Set(i => i.SortOrder, item.SortOrder)
            .Set(i => i.ItemType, item.ItemType)
            .Set(i => i.Label, item.Label)
            .Set(i => i.Configuration, item.Configuration)
            .Update();

        if (existing != null) {
            _auditLog.LogFieldChange("EventChecklistItem", item.Id, "SortOrder", existing.SortOrder.ToString(), item.SortOrder.ToString());
            _auditLog.LogFieldChange("EventChecklistItem", item.Id, "ItemType", existing.ItemType.ToString(), item.ItemType.ToString());
            _auditLog.LogFieldChange("EventChecklistItem", item.Id, "Label", existing.Label, item.Label);
            _auditLog.LogFieldChange("EventChecklistItem", item.Id, "Configuration", existing.Configuration, item.Configuration);
        }
    }

    public void SwapChecklistItemOrder(Guid eventId, Guid itemId, int direction) {
        var items = GetChecklistItems(eventId);
        var idx = items.FindIndex(i => i.Id == itemId);
        if (idx < 0)
            return;

        var targetIdx = idx + direction;
        if (targetIdx < 0 || targetIdx >= items.Count)
            return;

        var current = items[idx];
        var target = items[targetIdx];
        var tmpOrder = current.SortOrder;

        _context.EventChecklistItems
            .Where(i => i.Id == current.Id)
            .Set(i => i.SortOrder, target.SortOrder)
            .Update();

        _context.EventChecklistItems
            .Where(i => i.Id == target.Id)
            .Set(i => i.SortOrder, tmpOrder)
            .Update();
    }

    public void DeleteChecklistItem(Guid itemId) {
        _context.EventChecklistItems.Where(i => i.Id == itemId).Delete();
        _auditLog.LogDeleted("EventChecklistItem", itemId);
    }

    public void CheckItem(Guid itemId, Guid? resultId) {
        _context.EventChecklistItems
            .Where(i => i.Id == itemId)
            .Set(i => i.IsCompleted, true)
            .Set(i => i.CompletedAt, DateTime.UtcNow)
            .Set(i => i.ResultId, resultId)
            .Update();

        _auditLog.LogFieldChange("EventChecklistItem", itemId, "IsCompleted", "False", "True");
    }

    public void UncheckItem(Guid itemId) {
        _context.EventChecklistItems
            .Where(i => i.Id == itemId)
            .Set(i => i.IsCompleted, false)
            .Set(i => i.CompletedAt, (DateTime?)null)
            .Update();

        _auditLog.LogFieldChange("EventChecklistItem", itemId, "IsCompleted", "True", "False");
    }

    public EventTemplate? GetTemplate(Guid id)
        => _context.EventTemplates.Where(t => t.Id == id && t.DeletedAt == null).FirstOrDefault();

    public List<EventTemplate> GetAllTemplates()
        => _context.EventTemplates.Where(t => t.DeletedAt == null).OrderBy(t => t.Name).ToList();

    public void CreateTemplate(EventTemplate template) {
        _context.Insert(template);
        _auditLog.LogCreated("EventTemplate", template.Id);
    }

    public void DeleteTemplate(Guid id) {
        _context.EventTemplates.Where(t => t.Id == id).Delete();
    }

    public void SoftDelete(Guid id) {
        _context.Events.Where(x => x.Id == id).Set(x => x.DeletedAt, DateTime.UtcNow).Update();
        _auditLog.LogSoftDeleted("Event", id);
    }

    public void SoftDeleteTemplate(Guid id) {
        _context.EventTemplates.Where(x => x.Id == id).Set(x => x.DeletedAt, DateTime.UtcNow).Update();
        _auditLog.LogSoftDeleted("EventTemplate", id);
    }
}
