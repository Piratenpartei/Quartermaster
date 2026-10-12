using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.Events;

namespace Quartermaster.Data.AssemblyMotions;

public class AssemblyMotionPortalRepository {
    private readonly DbContext _context;
    private readonly AuditLogRepository _auditLog;

    public AssemblyMotionPortalRepository(DbContext context, AuditLogRepository auditLog) {
        _context = context;
        _auditLog = auditLog;
    }

    public AssemblyMotionPortal? Get(Guid id)
        => _context.AssemblyMotionPortals.Where(p => p.Id == id).FirstOrDefault();

    public AssemblyMotionPortal? GetByEvent(Guid eventId)
        => _context.AssemblyMotionPortals.Where(p => p.EventId == eventId).FirstOrDefault();

    public AssemblyMotionPortal Create(Guid eventId, DateTime opensAt, DateTime closesAt, DateTime nowUtc, Func<string, string>? templateFor = null) {
        var portal = new AssemblyMotionPortal {
            EventId = eventId,
            OpensAt = opensAt,
            ClosesAt = closesAt,
            CreatedAt = nowUtc
        };
        _context.Insert(portal);

        foreach (var type in AssemblyMotionDefaultTypes.Create(portal.Id, templateFor ?? (_ => "")))
            _context.Insert(type);

        _auditLog.LogCreated("AssemblyMotionPortal", portal.Id);
        return portal;
    }

    public void Update(AssemblyMotionPortal portal) {
        var existing = _context.AssemblyMotionPortals.Where(p => p.Id == portal.Id).FirstOrDefault();

        _context.AssemblyMotionPortals
            .Where(p => p.Id == portal.Id)
            .Set(p => p.OpensAt, portal.OpensAt)
            .Set(p => p.ClosesAt, portal.ClosesAt)
            .Set(p => p.IntroMarkdown, portal.IntroMarkdown)
            .Set(p => p.IntroHtml, portal.IntroHtml)
            .Set(p => p.PrioritizationEnabled, portal.PrioritizationEnabled)
            .Set(p => p.PrioritizationStartsAt, portal.PrioritizationStartsAt)
            .Set(p => p.PrioritizationEndsAt, portal.PrioritizationEndsAt)
            .Set(p => p.WikiMirrorEnabled, portal.WikiMirrorEnabled)
            .Set(p => p.WikiPagePrefix, portal.WikiPagePrefix)
            .Set(p => p.WikiAssemblyLabel, portal.WikiAssemblyLabel)
            .Set(p => p.OpenSlidesMeetingId, portal.OpenSlidesMeetingId)
            .Update();

        if (existing == null)
            return;

        _auditLog.LogFieldChange("AssemblyMotionPortal", portal.Id, nameof(AssemblyMotionPortal.OpensAt),
            existing.OpensAt.ToString("o"), portal.OpensAt.ToString("o"));
        _auditLog.LogFieldChange("AssemblyMotionPortal", portal.Id, nameof(AssemblyMotionPortal.ClosesAt),
            existing.ClosesAt.ToString("o"), portal.ClosesAt.ToString("o"));
        _auditLog.LogFieldChange("AssemblyMotionPortal", portal.Id, nameof(AssemblyMotionPortal.IntroMarkdown),
            existing.IntroMarkdown, portal.IntroMarkdown);
        _auditLog.LogFieldChange("AssemblyMotionPortal", portal.Id, nameof(AssemblyMotionPortal.PrioritizationEnabled),
            existing.PrioritizationEnabled.ToString(), portal.PrioritizationEnabled.ToString());
        _auditLog.LogFieldChange("AssemblyMotionPortal", portal.Id, nameof(AssemblyMotionPortal.PrioritizationStartsAt),
            existing.PrioritizationStartsAt?.ToString("o"), portal.PrioritizationStartsAt?.ToString("o"));
        _auditLog.LogFieldChange("AssemblyMotionPortal", portal.Id, nameof(AssemblyMotionPortal.PrioritizationEndsAt),
            existing.PrioritizationEndsAt?.ToString("o"), portal.PrioritizationEndsAt?.ToString("o"));
        _auditLog.LogFieldChange("AssemblyMotionPortal", portal.Id, nameof(AssemblyMotionPortal.WikiMirrorEnabled),
            existing.WikiMirrorEnabled.ToString(), portal.WikiMirrorEnabled.ToString());
        _auditLog.LogFieldChange("AssemblyMotionPortal", portal.Id, nameof(AssemblyMotionPortal.WikiPagePrefix),
            existing.WikiPagePrefix, portal.WikiPagePrefix);
        _auditLog.LogFieldChange("AssemblyMotionPortal", portal.Id, nameof(AssemblyMotionPortal.WikiAssemblyLabel),
            existing.WikiAssemblyLabel, portal.WikiAssemblyLabel);
        _auditLog.LogFieldChange("AssemblyMotionPortal", portal.Id, nameof(AssemblyMotionPortal.OpenSlidesMeetingId),
            existing.OpenSlidesMeetingId?.ToString(), portal.OpenSlidesMeetingId?.ToString());
    }

    public List<AssemblyMotionType> GetTypes(Guid portalId)
        => _context.AssemblyMotionTypes
            .Where(t => t.PortalId == portalId)
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.Code)
            .ToList();

    public AssemblyMotionType? GetType(Guid typeId)
        => _context.AssemblyMotionTypes.Where(t => t.Id == typeId).FirstOrDefault();

    public AssemblyMotionType CreateType(AssemblyMotionType type) {
        _context.Insert(type);
        _auditLog.LogCreated("AssemblyMotionType", type.Id);
        return type;
    }

    public void UpdateType(AssemblyMotionType type) {
        var existing = _context.AssemblyMotionTypes.Where(t => t.Id == type.Id).FirstOrDefault();

        _context.AssemblyMotionTypes
            .Where(t => t.Id == type.Id)
            .Set(t => t.Code, type.Code)
            .Set(t => t.Name, type.Name)
            .Set(t => t.SortOrder, type.SortOrder)
            .Set(t => t.MinSubmitters, type.MinSubmitters)
            .Set(t => t.Deadline, type.Deadline)
            .Set(t => t.CompetingDeadline, type.CompetingDeadline)
            .Set(t => t.RequiredMajority, type.RequiredMajority)
            .Set(t => t.WikiTemplate, type.WikiTemplate)
            .Set(t => t.OpenSlidesCategoryId, type.OpenSlidesCategoryId)
            .Update();

        if (existing == null)
            return;

        _auditLog.LogFieldChange("AssemblyMotionType", type.Id, nameof(AssemblyMotionType.Code), existing.Code, type.Code);
        _auditLog.LogFieldChange("AssemblyMotionType", type.Id, nameof(AssemblyMotionType.Name), existing.Name, type.Name);
        _auditLog.LogFieldChange("AssemblyMotionType", type.Id, nameof(AssemblyMotionType.SortOrder),
            existing.SortOrder.ToString(), type.SortOrder.ToString());
        _auditLog.LogFieldChange("AssemblyMotionType", type.Id, nameof(AssemblyMotionType.MinSubmitters),
            existing.MinSubmitters.ToString(), type.MinSubmitters.ToString());
        _auditLog.LogFieldChange("AssemblyMotionType", type.Id, nameof(AssemblyMotionType.Deadline),
            existing.Deadline?.ToString("o"), type.Deadline?.ToString("o"));
        _auditLog.LogFieldChange("AssemblyMotionType", type.Id, nameof(AssemblyMotionType.CompetingDeadline),
            existing.CompetingDeadline?.ToString("o"), type.CompetingDeadline?.ToString("o"));
        _auditLog.LogFieldChange("AssemblyMotionType", type.Id, nameof(AssemblyMotionType.RequiredMajority),
            existing.RequiredMajority.ToString(), type.RequiredMajority.ToString());
        _auditLog.LogFieldChange("AssemblyMotionType", type.Id, nameof(AssemblyMotionType.WikiTemplate),
            existing.WikiTemplate, type.WikiTemplate);
    }

    public bool TypeHasMotions(Guid typeId)
        => _context.AssemblyMotions.Any(m => m.TypeId == typeId && m.DeletedAt == null);

    public void DeleteType(Guid typeId) {
        _context.AssemblyMotionTypes.Where(t => t.Id == typeId).Delete();
        _auditLog.LogDeleted("AssemblyMotionType", typeId);
    }

    public List<AssemblyMotionPortal> GetForChapters(IReadOnlyCollection<Guid>? chapterIds) {
        var query = _context.AssemblyMotionPortals
            .InnerJoin(_context.Events, (p, e) => p.EventId == e.Id && e.DeletedAt == null, (p, e) => new { Portal = p, e.ChapterId });

        if (chapterIds != null)
            query = query.Where(x => chapterIds.Contains(x.ChapterId));

        return query.Select(x => x.Portal).ToList();
    }
}
