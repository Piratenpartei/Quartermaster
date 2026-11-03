using System;
using Quartermaster.Api.ChapterMails;
using Quartermaster.Api.Events;
using Quartermaster.Data;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.Chapters;
using Quartermaster.Server.ChapterMails;

namespace Quartermaster.Server.Events;

/// <summary>Keeps a checklist item's mail recipients and created motion inside the event's chapter and the chapters below it, the reach a chapter mail has.</summary>
public class ChecklistTargetValidator {
    private readonly ChapterRepository _chapters;
    private readonly AdministrativeDivisionRepository _divisions;
    private readonly DbContext _db;

    public ChecklistTargetValidator(ChapterRepository chapters, AdministrativeDivisionRepository divisions, DbContext db) {
        _chapters = chapters;
        _divisions = divisions;
        _db = db;
    }

    public bool IsWithinReach(Guid eventChapterId, EventChecklistItemConfigDTO? config) {
        if (config == null)
            return true;
        if (config.ChapterId != null && !_chapters.GetDescendantIds(eventChapterId).Contains(config.ChapterId.Value))
            return false;
        return IsMailTargetWithinReach(eventChapterId, config);
    }

    /// <summary>Target types other than Chapter and AdministrativeDivision resolve to no members, so they cannot leave the reach.</summary>
    private bool IsMailTargetWithinReach(Guid eventChapterId, EventChecklistItemConfigDTO config) {
        if (config.TargetId == null || config.TargetId == Guid.Empty)
            return true;

        ChapterMailTargetType targetType;
        if (config.TargetType == "Chapter") {
            targetType = ChapterMailTargetType.Chapter;
        } else if (config.TargetType == "AdministrativeDivision") {
            targetType = ChapterMailTargetType.AdministrativeDivision;
        } else {
            return true;
        }
        return ChapterMailTargetValidator.Validate(eventChapterId, targetType, config.TargetId.Value, _chapters, _divisions, _db) == null;
    }
}
