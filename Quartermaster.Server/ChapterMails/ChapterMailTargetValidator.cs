using System;
using System.Linq;
using LinqToDB;
using Quartermaster.Api.ChapterMails;
using Quartermaster.Data;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.Chapters;

namespace Quartermaster.Server.ChapterMails;

/// <summary>Enforces that a chapter mail's target stays inside the owning chapter's reach: own chapter + descendants for Chapter targets, the chapter's assigned administrative divisions + their descendants for AdministrativeDivision targets.</summary>
public static class ChapterMailTargetValidator {
    public static string? Validate(Guid chapterId, ChapterMailTargetType targetType, Guid targetId,
        ChapterRepository chapterRepo, AdministrativeDivisionRepository adminDivRepo, DbContext db) {

        if (targetType == ChapterMailTargetType.Chapter) {
            var allowed = chapterRepo.GetDescendantIds(chapterId);
            if (!allowed.Contains(targetId))
                return "target chapter must be the owning chapter or a descendant";
            return null;
        }

        if (targetType == ChapterMailTargetType.AdministrativeDivision) {
            var ownDivisionIds = db.ChapterAdministrativeDivisions
                .Where(c => c.ChapterId == chapterId)
                .Select(c => c.AdministrativeDivisionId)
                .ToList();
            if (ownDivisionIds.Count == 0)
                return "owning chapter has no assigned administrative division to target";

            foreach (var divId in ownDivisionIds) {
                if (adminDivRepo.GetDescendantIds(divId).Contains(targetId))
                    return null;
            }
            return "target administrative division must be the chapter's assigned division or a descendant";
        }

        return $"unknown target type {targetType}";
    }
}
