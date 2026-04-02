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

        if (HasGlobalPermission(userId, permission, globalPermRepo))
            return null;

        var allChapterPerms = chapterPermRepo.GetAllForUser(userId);
        var permitted = new HashSet<Guid>();

        foreach (var (chapterId, identifiers) in allChapterPerms) {
            if (!identifiers.Contains(permission))
                continue;

            foreach (var descendantId in chapterRepo.GetDescendantIds(chapterId))
                permitted.Add(descendantId);
        }

        return permitted.ToList();
    }
}
