using LinqToDB;
using Quartermaster.Api.Events;
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

    /// <summary>
    /// Recomputes the event's status based on checklist completion and event date.
    /// Applies only auto-transitions that promote status forward (Draft→Active→Completed).
    /// Never auto-archives; never demotes a manually-set status.
    /// Returns the updated event.
    /// </summary>
    public Event? RefreshStatus(Guid id) {
        var ev = Get(id);
        if (ev == null)
            return null;

        // Archived is terminal for auto-transitions
        if (ev.Status == EventStatus.Archived)
            return ev;

        var items = GetChecklistItems(id);
        var anyCompleted = items.Any(i => i.IsCompleted);
        var allCompleted = items.Count > 0 && items.All(i => i.IsCompleted);
        var dateInPast = ev.EventDate.HasValue && ev.EventDate.Value < DateTime.UtcNow;

        var newStatus = ev.Status;

        // Draft → Active: first item checked
        if (ev.Status == EventStatus.Draft && anyCompleted)
            newStatus = EventStatus.Active;

        // Active → Completed: all items completed AND date passed
        if (newStatus == EventStatus.Active && allCompleted && dateInPast)
            newStatus = EventStatus.Completed;

        if (newStatus != ev.Status) {
            SetStatus(id, newStatus);
            ev.Status = newStatus;
        }

        return ev;
    }

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
            .Set(e => e.Visibility, ev.Visibility)
            .Update();

        if (existing != null) {
            _auditLog.LogFieldChange("Event", ev.Id, "InternalName", existing.InternalName, ev.InternalName);
            _auditLog.LogFieldChange("Event", ev.Id, "PublicName", existing.PublicName, ev.PublicName);
            _auditLog.LogFieldChange("Event", ev.Id, "Description", existing.Description, ev.Description);
            _auditLog.LogFieldChange("Event", ev.Id, "EventDate", existing.EventDate?.ToString("o"), ev.EventDate?.ToString("o"));
            _auditLog.LogFieldChange("Event", ev.Id, "Visibility", existing.Visibility.ToString(), ev.Visibility.ToString());
        }
    }

    public void SetStatus(Guid id, EventStatus status) {
        var existing = _context.Events.Where(e => e.Id == id).FirstOrDefault();

        _context.Events
            .Where(e => e.Id == id)
            .Set(e => e.Status, status)
            .Update();

        _auditLog.LogFieldChange("Event", id, "Status", existing?.Status.ToString(), status.ToString());
    }

    public void SetVisibility(Guid id, EventVisibility visibility) {
        var existing = _context.Events.Where(e => e.Id == id).FirstOrDefault();

        _context.Events
            .Where(e => e.Id == id)
            .Set(e => e.Visibility, visibility)
            .Update();

        _auditLog.LogFieldChange("Event", id, "Visibility", existing?.Visibility.ToString(), visibility.ToString());
    }

    public (List<Event> Items, int TotalCount) Search(Guid? chapterId, bool includeArchived, int page, int pageSize,
        List<EventVisibility>? allowedVisibilities = null) {
        var q = _context.Events.Where(e => e.DeletedAt == null).AsQueryable();

        if (chapterId.HasValue)
            q = q.Where(e => e.ChapterId == chapterId.Value);

        if (!includeArchived)
            q = q.Where(e => e.Status != EventStatus.Archived);

        if (allowedVisibilities != null)
            q = q.Where(e => allowedVisibilities.Contains(e.Visibility));

        var totalCount = q.Count();
        var items = q.OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (items, totalCount);
    }

    public List<Event> GetUpcoming(List<EventVisibility>? allowedVisibilities, List<Guid>? allowedChapterIds, int limit) {
        var now = DateTime.UtcNow;
        var q = _context.Events
            .Where(e => e.DeletedAt == null
                && e.Status != EventStatus.Archived
                && e.EventDate != null
                && e.EventDate >= now);

        if (allowedVisibilities != null)
            q = q.Where(e => allowedVisibilities.Contains(e.Visibility));

        if (allowedChapterIds != null)
            q = q.Where(e => allowedChapterIds.Contains(e.ChapterId));

        return q.OrderBy(e => e.EventDate).Take(limit).ToList();
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

    public List<EventTemplate> GetAllTemplates(List<Guid>? allowedChapterIds = null) {
        var q = _context.EventTemplates.Where(t => t.DeletedAt == null);

        if (allowedChapterIds != null)
            q = q.Where(t => t.ChapterId != null && allowedChapterIds.Contains(t.ChapterId.Value));

        return q.OrderBy(t => t.Name).ToList();
    }

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
