using Quartermaster.Data.Chapters;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace Quartermaster.Server.Authentication;

public static class EndpointAuthorizationHelper {
    public static Guid? GetUserId(ClaimsPrincipal user) {
        var claim = user.FindFirst(ClaimTypes.NameIdentifier);
        if (claim != null && Guid.TryParse(claim.Value, out var id))
            return id;
        return null;
    }

    public static bool HasGlobalPermission(Guid userId, string permission, UserGlobalPermissionRepository repo) {
        return repo.GetForUser(userId).Any(p => p.Identifier == permission);
    }

    /// <summary>
    /// Checks whether the user has the given permission, either globally or on the specified chapter
    /// (with inheritance). Combines the two-step check into a single call.
    /// </summary>
    public static bool HasPermission(
        Guid userId, Guid chapterId, string permission,
        UserGlobalPermissionRepository globalPermRepo,
        UserChapterPermissionRepository chapterPermRepo,
        ChapterRepository chapterRepo) {

        return HasGlobalPermission(userId, permission, globalPermRepo) ||
            chapterPermRepo.HasPermissionWithInheritance(userId, chapterId, permission, chapterRepo);
    }

    /// <summary>
    /// Checks whether the user has the given permission, using different identifiers for the
    /// global check and the chapter-scoped check (e.g., ViewAllMembers globally vs ViewMembers per chapter).
    /// </summary>
    public static bool HasPermission(
        Guid userId, Guid chapterId, string globalPermission, string chapterPermission,
        UserGlobalPermissionRepository globalPermRepo,
        UserChapterPermissionRepository chapterPermRepo,
        ChapterRepository chapterRepo) {

        return HasGlobalPermission(userId, globalPermission, globalPermRepo) ||
            chapterPermRepo.HasPermissionWithInheritance(userId, chapterId, chapterPermission, chapterRepo);
    }

    /// <summary>
    /// Returns the set of chapter IDs the user is permitted to access for a given permission.
    /// Returns null if the user has the global permission (meaning all chapters).
    /// Returns an empty list if the user has no permission at all.
    /// </summary>
    public static List<Guid>? GetPermittedChapterIds(
        Guid userId,
        string permission,
        UserGlobalPermissionRepository globalPermRepo,
        UserChapterPermissionRepository chapterPermRepo,
        ChapterRepository chapterRepo) {

        return GetPermittedChapterIds(userId, permission, permission, globalPermRepo, chapterPermRepo, chapterRepo);
    }

    /// <summary>
    /// Returns the set of chapter IDs the user is permitted to access.
    /// Uses globalPermission for the global check and chapterPermission for the chapter-scoped check.
    /// Returns null if the user has the global permission (meaning all chapters).
    /// Returns an empty list if the user has no permission at all.
    /// </summary>
    public static List<Guid>? GetPermittedChapterIds(
        Guid userId,
        string globalPermission,
        string chapterPermission,
        UserGlobalPermissionRepository globalPermRepo,
        UserChapterPermissionRepository chapterPermRepo,
        ChapterRepository chapterRepo) {

        if (HasGlobalPermission(userId, globalPermission, globalPermRepo))
            return null;

        var allChapterPerms = chapterPermRepo.GetAllForUser(userId);
        var permitted = new HashSet<Guid>();

        foreach (var (chapterId, identifiers) in allChapterPerms) {
            if (!identifiers.Contains(chapterPermission))
                continue;

            foreach (var descendantId in chapterRepo.GetDescendantIds(chapterId))
                permitted.Add(descendantId);
        }

        return permitted.ToList();
    }
}
