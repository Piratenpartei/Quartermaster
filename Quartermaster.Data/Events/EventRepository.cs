using LinqToDB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Data.Events;

public class EventRepository {
    private readonly DbContext _context;

    public EventRepository(DbContext context) {
        _context = context;
    }

    public Event? Get(Guid id)
        => _context.Events.Where(e => e.Id == id).FirstOrDefault();

    public void Create(Event ev) => _context.Insert(ev);

    public void Update(Event ev) {
        _context.Events
            .Where(e => e.Id == ev.Id)
            .Set(e => e.InternalName, ev.InternalName)
            .Set(e => e.PublicName, ev.PublicName)
            .Set(e => e.Description, ev.Description)
            .Set(e => e.EventDate, ev.EventDate)
            .Update();
    }

    public void SetArchived(Guid id, bool archived) {
        _context.Events
            .Where(e => e.Id == id)
            .Set(e => e.IsArchived, archived)
            .Update();
    }

    public (List<Event> Items, int TotalCount) Search(Guid? chapterId, bool includeArchived, int page, int pageSize) {
        var q = _context.Events.AsQueryable();

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

    public void CreateChecklistItem(EventChecklistItem item) => _context.Insert(item);

    public void UpdateChecklistItem(EventChecklistItem item) {
        _context.EventChecklistItems
            .Where(i => i.Id == item.Id)
            .Set(i => i.SortOrder, item.SortOrder)
            .Set(i => i.ItemType, item.ItemType)
            .Set(i => i.Label, item.Label)
            .Set(i => i.Configuration, item.Configuration)
            .Update();
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
    }

    public void CheckItem(Guid itemId, Guid? resultId) {
        _context.EventChecklistItems
            .Where(i => i.Id == itemId)
            .Set(i => i.IsCompleted, true)
            .Set(i => i.CompletedAt, DateTime.UtcNow)
            .Set(i => i.ResultId, resultId)
            .Update();
    }

    public void UncheckItem(Guid itemId) {
        _context.EventChecklistItems
            .Where(i => i.Id == itemId)
            .Set(i => i.IsCompleted, false)
            .Set(i => i.CompletedAt, (DateTime?)null)
            .Update();
    }

    public EventTemplate? GetTemplate(Guid id)
        => _context.EventTemplates.Where(t => t.Id == id).FirstOrDefault();

    public List<EventTemplate> GetAllTemplates()
        => _context.EventTemplates.OrderBy(t => t.Name).ToList();

    public void CreateTemplate(EventTemplate template) => _context.Insert(template);

    public void DeleteTemplate(Guid id) {
        _context.EventTemplates.Where(t => t.Id == id).Delete();
    }
}
