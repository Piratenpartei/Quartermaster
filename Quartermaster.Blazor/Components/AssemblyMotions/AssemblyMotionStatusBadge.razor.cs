using Microsoft.AspNetCore.Components;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Components.AssemblyMotions;

public partial class AssemblyMotionStatusBadge {
    [Parameter, EditorRequired]
    public AssemblyMotionStatus Status { get; set; }

    [Parameter]
    public bool IsInRevision { get; set; }

    [Parameter]
    public string? Class { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    private string CssClass => IsInRevision ? "border-warning text-warning-emphasis" : Status switch {
        AssemblyMotionStatus.Draft => "border-secondary text-secondary-emphasis",
        AssemblyMotionStatus.CollectingCoSubmitters => "border-info text-info-emphasis",
        AssemblyMotionStatus.Submitted => "border-primary text-primary-emphasis",
        AssemblyMotionStatus.Admitted => "border-success text-success-emphasis",
        AssemblyMotionStatus.FormallyInsufficient => "border-danger text-danger-emphasis",
        AssemblyMotionStatus.Withdrawn => "border-secondary text-secondary-emphasis",
        AssemblyMotionStatus.Lapsed => "border-secondary text-secondary-emphasis",
        _ => "border-secondary text-secondary-emphasis"
    };

    private string Label => AssemblyMotionDisplay.StatusLabel(I18n, Status, IsInRevision);
}
