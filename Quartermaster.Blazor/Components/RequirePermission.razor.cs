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

    private bool IsVisible() {
        if (RequireAuth && !AuthService.IsAuthenticated)
            return false;

        if (!string.IsNullOrEmpty(GlobalPermission) && !AuthService.HasGlobalPermission(GlobalPermission))
            return false;

        if (!string.IsNullOrEmpty(AnyChapterPermission)) {
            if (!AuthService.IsAuthenticated)
                return false;

            // Admin override: global permission grants access everywhere
            if (AuthService.HasGlobalPermission(AnyChapterPermission))
                return true;

            var chapters = AuthService.Permissions?.Chapters;
            if (chapters == null)
                return false;

            foreach (var (_, perms) in chapters) {
                if (perms.Contains(AnyChapterPermission))
                    return true;
            }

            return false;
        }

        return true;
    }
}
