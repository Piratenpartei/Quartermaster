using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Components;

public enum PublicFormKind {
    MembershipApplication,
    DueSelection,
    Motion
}

/// <summary>
/// Shows the page only while one of <see cref="Forms"/> is switched on. Officers who process applications or due
/// selections still get those forms, so they can enter paper submissions; the server applies the same rule.
/// </summary>
public partial class PublicFormGate {
    [Inject]
    public required ClientConfigService Config { get; set; }

    [Inject]
    public required AuthService AuthService { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public IReadOnlyList<PublicFormKind> Forms { get; set; } = [];

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>The same rule decides the menu entries.</summary>
    public static bool IsOpen(PublicFormKind form, ClientConfigService config, AuthService auth) {
        return form switch {
            PublicFormKind.MembershipApplication => config.MembershipApplicationEnabled || auth.HasPermissionInAnyChapter(PermissionIdentifier.ProcessApplications),
            PublicFormKind.DueSelection => config.DueSelectionEnabled || auth.HasPermissionInAnyChapter(PermissionIdentifier.ProcessDueSelections),
            _ => config.MotionFormEnabled
        };
    }

    private bool IsOpen() {
        return Forms.Any(f => IsOpen(f, Config, AuthService));
    }
}
