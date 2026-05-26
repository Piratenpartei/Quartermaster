using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;

namespace Quartermaster.Server.Authentication;

/// <summary>
/// Request-scoped facade over the three permission repositories. Endpoints inject
/// one <see cref="PermissionContext"/> instead of three repositories + a static helper,
/// and permission checks become 1-arg / 2-arg calls.
/// <para>
/// The <see cref="UserId"/> is resolved lazily from the current <see cref="HttpContext"/>'s
/// authenticated principal and cached for the lifetime of the request. For non-HTTP
/// callers (notably SignalR hubs, where <c>HttpContext</c> is null but <c>Context.User</c>
/// is populated), call <see cref="Bind"/> with the principal first.
/// </para>
/// </summary>
public class PermissionContext {
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;
    private readonly ChapterRepository _chapterRepo;

    private Guid? _cachedUserId;
    private bool _userIdResolved;

    public PermissionContext(
        IHttpContextAccessor httpContextAccessor,
        UserGlobalPermissionRepository globalPermRepo,
        UserChapterPermissionRepository chapterPermRepo,
        ChapterRepository chapterRepo) {
        _httpContextAccessor = httpContextAccessor;
        _globalPermRepo = globalPermRepo;
        _chapterPermRepo = chapterPermRepo;
        _chapterRepo = chapterRepo;
    }

    /// <summary>The authenticated caller's user ID, or <c>null</c> if the request is anonymous.</summary>
    public Guid? UserId {
        get {
            if (_userIdResolved)
                return _cachedUserId;
            _userIdResolved = true;
            var principal = _httpContextAccessor.HttpContext?.User;
            var claim = principal?.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && Guid.TryParse(claim.Value, out var id))
                _cachedUserId = id;
            return _cachedUserId;
        }
    }

    /// <summary>
    /// Bind the principal explicitly when no <see cref="HttpContext"/> is available
    /// (e.g. from inside a SignalR <c>Hub</c> method, where <c>IHttpContextAccessor.HttpContext</c>
    /// is null but <c>Context.User</c> carries the authenticated identity). Must be
    /// called before any permission check on this instance.
    /// </summary>
    public void Bind(ClaimsPrincipal principal) {
        _userIdResolved = true;
        var claim = principal.FindFirst(ClaimTypes.NameIdentifier);
        _cachedUserId = claim != null && Guid.TryParse(claim.Value, out var id) ? id : null;
    }

    /// <summary>True when the caller is authenticated and holds the given global permission.</summary>
    public bool HasGlobal(string permission) {
        var uid = UserId;
        if (uid == null)
            return false;
        return _globalPermRepo.GetForUser(uid.Value).Any(p => p.Identifier == permission);
    }

    /// <summary>
    /// True when the caller is authenticated and holds the given permission either globally
    /// or on the specified chapter (with ancestor inheritance).
    /// </summary>
    public bool Has(Guid chapterId, string permission) {
        var uid = UserId;
        if (uid == null)
            return false;
        if (HasGlobal(permission))
            return true;
        return _chapterPermRepo.HasPermissionWithInheritance(uid.Value, chapterId, permission, _chapterRepo);
    }

    /// <summary>
    /// True when the caller is authenticated and holds the given permission on the EXACT
    /// chapter (no ancestor inheritance). Use for chapter-bound privileges (e.g. voting)
    /// where a parent-chapter holder should NOT inherit into child chapters.
    /// </summary>
    public bool HasExact(Guid chapterId, string permission) {
        var uid = UserId;
        if (uid == null)
            return false;
        return _chapterPermRepo.HasPermissionForChapter(uid.Value, chapterId, permission);
    }

    /// <summary>
    /// True when the caller is authenticated and holds either <paramref name="globalPermission"/>
    /// globally or <paramref name="chapterPermission"/> on the specified chapter (with inheritance).
    /// Used by view-vs-view-all and similar pairings.
    /// </summary>
    public bool Has(Guid chapterId, string globalPermission, string chapterPermission) {
        var uid = UserId;
        if (uid == null)
            return false;
        if (HasGlobal(globalPermission))
            return true;
        return _chapterPermRepo.HasPermissionWithInheritance(uid.Value, chapterId, chapterPermission, _chapterRepo);
    }

    /// <summary>
    /// Returns the set of chapter IDs (including inheriting descendants) the caller may act on
    /// for the given permission. Returns <c>null</c> when the caller holds the global form
    /// (i.e. all chapters); an empty list when nothing is permitted.
    /// </summary>
    public List<Guid>? GetPermittedChapterIds(string globalPermission, string chapterPermission) {
        var uid = UserId;
        if (uid == null)
            return new List<Guid>();
        if (HasGlobal(globalPermission))
            return null;

        var allChapterPerms = _chapterPermRepo.GetAllForUser(uid.Value);
        var permitted = new HashSet<Guid>();
        foreach (var (chapterId, identifiers) in allChapterPerms) {
            if (!identifiers.Contains(chapterPermission))
                continue;
            foreach (var descendantId in _chapterRepo.GetDescendantIds(chapterId))
                permitted.Add(descendantId);
        }
        return permitted.ToList();
    }

    /// <summary>Single-permission overload of <see cref="GetPermittedChapterIds(string, string)"/>.</summary>
    public List<Guid>? GetPermittedChapterIds(string permission)
        => GetPermittedChapterIds(permission, permission);
}
