using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;
using Quartermaster.Data.AuditLog;

namespace Quartermaster.Data.Meetings;

public class AgendaItemRepository {
    public const int MaxDepth = 3; // root=1, subitem=2, sub-subitem=3

    private readonly DbContext _context;
    private readonly AuditLogRepository _auditLog;

    public AgendaItemRepository(DbContext context, AuditLogRepository auditLog) {
        _context = context;
        _auditLog = auditLog;
    }

    public AgendaItem? Get(Guid id)
        => _context.AgendaItems.Where(a => a.Id == id).FirstOrDefault();

    public List<AgendaItem> GetForMeeting(Guid meetingId)
        => _context.AgendaItems.Where(a => a.MeetingId == meetingId)
            .OrderBy(a => a.SortOrder).ToList();

    public List<AgendaItem> GetChildren(Guid parentId)
        => _context.AgendaItems.Where(a => a.ParentId == parentId)
            .OrderBy(a => a.SortOrder).ToList();

    public List<AgendaItem> GetSiblings(Guid meetingId, Guid? parentId)
        => _context.AgendaItems.Where(a => a.MeetingId == meetingId && a.ParentId == parentId)
            .OrderBy(a => a.SortOrder).ToList();

    /// <summary>
    /// Returns the depth (1-based) of an item. Root items are depth 1.
    /// </summary>
    public int GetDepth(Guid itemId) {
        var depth = 0;
        var current = Get(itemId);
        while (current != null) {
            depth++;
            if (current.ParentId == null || depth > MaxDepth + 10)
                break;
            current = Get(current.ParentId.Value);
        }
        return depth;
    }

    /// <summary>
    /// True if making <paramref name="candidateParentId"/> the new parent of <paramref name="itemId"/>
    /// would create a cycle (the candidate is itemId itself or a descendant of itemId).
    /// </summary>
    public bool WouldCreateCycle(Guid itemId, Guid candidateParentId) {
        if (itemId == candidateParentId)
            return true;
        var current = Get(candidateParentId);
        var guard = 0;
        while (current != null && guard++ < 100) {
            if (current.Id == itemId)
                return true;
            if (current.ParentId == null)
                return false;
            current = Get(current.ParentId.Value);
        }
        return false;
    }

    public AgendaItem Create(AgendaItem item) {
        if (item.Id == Guid.Empty)
            item.Id = Guid.NewGuid();
        // Append at the end of its sibling list.
        var maxSibling = _context.AgendaItems
            .Where(a => a.MeetingId == item.MeetingId && a.ParentId == item.ParentId)
            .Select(a => (int?)a.SortOrder).Max();
        item.SortOrder = (maxSibling ?? -1) + 1;
        _context.Insert(item);
        _auditLog.LogCreated("AgendaItem", item.Id);
        return item;
    }

    public void Update(AgendaItem updated) {
        var existing = Get(updated.Id);
        if (existing == null)
            return;
        _context.AgendaItems.Where(a => a.Id == updated.Id)
            .Set(a => a.Title, updated.Title)
            .Set(a => a.ItemType, updated.ItemType)
            .Set(a => a.MotionId, updated.MotionId)
            .Set(a => a.Notes, updated.Notes)
            .Set(a => a.Resolution, updated.Resolution)
            .Update();
        if (existing.Title != updated.Title)
            _auditLog.LogFieldChange("AgendaItem", updated.Id, "Title", existing.Title, updated.Title);
        if (existing.ItemType != updated.ItemType)
            _auditLog.LogFieldChange("AgendaItem", updated.Id, "ItemType", existing.ItemType.ToString(), updated.ItemType.ToString());
        if (existing.MotionId != updated.MotionId)
            _auditLog.LogFieldChange("AgendaItem", updated.Id, "MotionId", existing.MotionId?.ToString(), updated.MotionId?.ToString());
    }

    /// <summary>
    /// Partial update of Notes field only — for fast auto-save during minute-taking.
    /// Does not emit an audit log entry (notes are free-form and change frequently).
    /// </summary>
    public void UpdateNotes(Guid itemId, string? notes) {
        _context.AgendaItems.Where(a => a.Id == itemId)
            .Set(a => a.Notes, notes).Update();
    }

    /// <summary>
    /// Updates the Resolution field. Used by the meeting-complete auto-resolve sweep
    /// to fill in per-item vote tallies.
    /// </summary>
    public void UpdateResolution(Guid itemId, string? resolution) {
        _context.AgendaItems.Where(a => a.Id == itemId)
            .Set(a => a.Resolution, resolution).Update();
    }

    public void Delete(Guid itemId) {
        _context.AgendaItems.Where(a => a.Id == itemId).Delete();
        _auditLog.LogDeleted("AgendaItem", itemId);
    }

    /// <summary>
    /// Swap the item's SortOrder with its sibling in the given direction (-1 = up, +1 = down).
    /// </summary>
    public void Reorder(Guid itemId, int direction) {
        var item = Get(itemId);
        if (item == null || (direction != -1 && direction != 1))
            return;

        var siblings = GetSiblings(item.MeetingId, item.ParentId);
        var index = siblings.FindIndex(s => s.Id == itemId);
        if (index < 0)
            return;
        var swapIndex = index + direction;
        if (swapIndex < 0 || swapIndex >= siblings.Count)
            return;
        var neighbor = siblings[swapIndex];

        _context.AgendaItems.Where(a => a.Id == item.Id)
            .Set(a => a.SortOrder, neighbor.SortOrder).Update();
        _context.AgendaItems.Where(a => a.Id == neighbor.Id)
            .Set(a => a.SortOrder, item.SortOrder).Update();
    }

    /// <summary>
    /// Reparent an item to a new parent (or to root if newParentId is null).
    /// Appends the item to the end of the new parent's sibling list.
    /// Caller must have already validated no-cycle + depth constraints.
    /// </summary>
    public void Move(Guid itemId, Guid? newParentId) {
        var item = Get(itemId);
        if (item == null)
            return;
        var maxSibling = _context.AgendaItems
            .Where(a => a.MeetingId == item.MeetingId && a.ParentId == newParentId)
            .Select(a => (int?)a.SortOrder).Max();
        var newSort = (maxSibling ?? -1) + 1;
        _context.AgendaItems.Where(a => a.Id == itemId)
            .Set(a => a.ParentId, newParentId)
            .Set(a => a.SortOrder, newSort).Update();
        _auditLog.LogFieldChange("AgendaItem", itemId, "ParentId",
            item.ParentId?.ToString(), newParentId?.ToString());
    }

    public void MarkStarted(Guid itemId) {
        _context.AgendaItems.Where(a => a.Id == itemId)
            .Set(a => a.StartedAt, DateTime.UtcNow).Update();
    }

    public void MarkCompleted(Guid itemId) {
        _context.AgendaItems.Where(a => a.Id == itemId)
            .Set(a => a.CompletedAt, DateTime.UtcNow).Update();
    }

    /// <summary>
    /// Auto-complete any in-progress items (StartedAt set, CompletedAt null) in the meeting
    /// other than <paramref name="exceptItemId"/>. Used when starting a new item so only one
    /// item is active at a time.
    /// </summary>
    public void CompleteAllInProgressExcept(Guid meetingId, Guid exceptItemId) {
        var now = DateTime.UtcNow;
        _context.AgendaItems
            .Where(a => a.MeetingId == meetingId
                && a.Id != exceptItemId
                && a.StartedAt != null
                && a.CompletedAt == null)
            .Set(a => a.CompletedAt, now).Update();
    }

    /// <summary>
    /// Resets CompletedAt to null, effectively re-opening a completed agenda item.
    /// Used when meetings jump around and a previously-completed item needs revisiting.
    /// </summary>
    public void ResetCompletion(Guid itemId) {
        _context.AgendaItems.Where(a => a.Id == itemId)
            .Set(a => a.CompletedAt, (DateTime?)null).Update();
    }
}
