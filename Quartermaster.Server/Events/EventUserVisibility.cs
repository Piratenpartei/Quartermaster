using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Events;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.AssemblyMotions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Events;

/// <summary>Applies the event page's visibility rule to any user, for jobs that act outside that user's request.</summary>
public class EventUserVisibility {
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;
    private readonly ChapterRepository _chapterRepo;

    public EventUserVisibility(
        UserGlobalPermissionRepository globalPermRepo,
        UserChapterPermissionRepository chapterPermRepo,
        ChapterRepository chapterRepo) {
        _globalPermRepo = globalPermRepo;
        _chapterPermRepo = chapterPermRepo;
        _chapterRepo = chapterRepo;
    }

    public bool IsVisibleTo(Event ev, Guid userId) {
        var perms = new PermissionContext(new HttpContextAccessor(), _globalPermRepo, _chapterPermRepo, _chapterRepo);
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "EventUserVisibility");
        perms.Bind(new ClaimsPrincipal(identity));
        return AssemblyMotionEventVisibility.IsVisible(ev, perms);
    }
}
