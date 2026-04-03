using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Components;

public partial class RequirePermission {
    [Inject]
    public required AuthService AuthService { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public bool RequireAuth { get; set; }

    [Parameter]
    public string? GlobalPermission { get; set; }

    [Parameter]
    public string? AnyChapterPermission { get; set; }

    /// <summary>
    /// Show if user has ANY of these chapter-scoped permissions (logical OR).
    /// </summary>
    [Parameter]
    public List<string>? AnyOfChapterPermissions { get; set; }

    /// <summary>
    /// Show if user has ANY of these permissions — checks both global and chapter-scoped (logical OR).
    /// </summary>
    [Parameter]
    public List<string>? AnyOfPermissions { get; set; }

    private bool IsVisible() {
        if (RequireAuth && !AuthService.IsAuthenticated)
            return false;

        if (!string.IsNullOrEmpty(GlobalPermission) && !AuthService.HasGlobalPermission(GlobalPermission))
            return false;

        if (!string.IsNullOrEmpty(AnyChapterPermission)) {
            if (!HasAnyChapterPermission(AnyChapterPermission))
                return false;
        }

        if (AnyOfChapterPermissions is { Count: > 0 }) {
            if (!AuthService.IsAuthenticated)
                return false;

            var found = false;
            foreach (var perm in AnyOfChapterPermissions) {
                if (HasAnyChapterPermission(perm)) {
                    found = true;
                    break;
                }
            }

            if (!found)
                return false;
        }

        if (AnyOfPermissions is { Count: > 0 }) {
            if (!AuthService.IsAuthenticated)
                return false;

            var found = false;
            foreach (var perm in AnyOfPermissions) {
                if (AuthService.HasGlobalPermission(perm) || HasAnyChapterPermission(perm)) {
                    found = true;
                    break;
                }
            }

            if (!found)
                return false;
        }

        return true;
    }

    private bool HasAnyChapterPermission(string permission) {
        if (!AuthService.IsAuthenticated)
            return false;

        if (AuthService.HasGlobalPermission(permission))
            return true;

        var chapters = AuthService.Permissions?.Chapters;
        if (chapters == null)
            return false;

        foreach (var (_, perms) in chapters) {
            if (perms.Contains(permission))
                return true;
        }

        return false;
    }
}
