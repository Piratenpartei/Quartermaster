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
/// Request-scoped facade over the three permission repositories. <see cref="UserId"/>
/// is resolved lazily from <see cref="HttpContext"/>; SignalR hubs (HttpContext is null
/// there) must call <see cref="Bind"/> with <c>Context.User</c> first.
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

    /// <summary>Explicit principal binding for non-HTTP callers (SignalR hubs). Call before any permission check.</summary>
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

    /// <summary>True if authenticated and holds the permission globally or on <paramref name="chapterId"/> (with inheritance).</summary>
    public bool Has(Guid chapterId, string permission) {
        var uid = UserId;
        if (uid == null)
            return false;
        if (HasGlobal(permission))
            return true;
        return _chapterPermRepo.HasPermissionWithInheritance(uid.Value, chapterId, permission, _chapterRepo);
    }

    /// <summary>
    /// True if authenticated and holds the permission on the exact chapter — no inheritance.
    /// Use for chapter-bound privileges (voting) where parent-chapter holders must NOT inherit.
    /// </summary>
    public bool HasExact(Guid chapterId, string permission) {
        var uid = UserId;
        if (uid == null)
            return false;
        return _chapterPermRepo.HasPermissionForChapter(uid.Value, chapterId, permission);
    }

    /// <summary>Two-permission overload for view-vs-view-all pairings.</summary>
    public bool Has(Guid chapterId, string globalPermission, string chapterPermission) {
        var uid = UserId;
        if (uid == null)
            return false;
        if (HasGlobal(globalPermission))
            return true;
        return _chapterPermRepo.HasPermissionWithInheritance(uid.Value, chapterId, chapterPermission, _chapterRepo);
    }

    /// <summary>
    /// Chapter IDs (and inheriting descendants) the caller may act on. <c>null</c> = all chapters
    /// (global form held); empty list = nothing.
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
