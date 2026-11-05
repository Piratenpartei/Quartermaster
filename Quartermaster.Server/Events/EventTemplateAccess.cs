using System;
using System.Collections.Generic;
using System.Linq;
using Quartermaster.Api;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Events;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Events;

/// <summary>
/// Event templates are seen both ways: with <c>templates_view</c> a member sees the templates of their chapters, of the chapters below and
/// of the chapters above, so a higher chapter publishes a template and the lower ones use it. Editing works top down like the event rights.
/// Templates without a chapter are system or meta templates and need the global right.
/// </summary>
public class EventTemplateAccess {
    private readonly PermissionContext _perms;
    private readonly ChapterRepository _chapters;

    public EventTemplateAccess(PermissionContext perms, ChapterRepository chapters) {
        _perms = perms;
        _chapters = chapters;
    }

    /// <summary>The chapters whose templates the caller sees; null for all, templates without a chapter included.</summary>
    public List<Guid>? VisibleChapterIds() {
        var downwards = _perms.GetPermittedChapterIds(PermissionIdentifier.ViewTemplates);
        if (downwards == null)
            return null;

        var visible = new HashSet<Guid>(downwards);
        foreach (var chapterId in _perms.GetGrantedChapterIds(PermissionIdentifier.ViewTemplates)) {
            foreach (var ancestorId in _chapters.GetAncestorChainIds(chapterId))
                visible.Add(ancestorId);
        }
        return visible.ToList();
    }

    public bool CanView(EventTemplate template) {
        if (template.ChapterId == null)
            return _perms.HasGlobal(PermissionIdentifier.ViewTemplates);
        var visible = VisibleChapterIds();
        return visible == null || visible.Contains(template.ChapterId.Value);
    }

    public bool CanEdit(EventTemplate template) {
        if (template.ChapterId == null)
            return _perms.HasGlobal(PermissionIdentifier.EditTemplates);
        return CanEditTemplatesOf(template.ChapterId.Value);
    }

    public bool CanEditTemplatesOf(Guid chapterId) {
        return _perms.HasOnChapterOrAncestors(chapterId, PermissionIdentifier.EditTemplates);
    }
}
