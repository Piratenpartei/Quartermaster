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

    private StatusTone Tone => IsInRevision ? StatusTone.Warning : Status switch {
        AssemblyMotionStatus.CollectingCoSubmitters => StatusTone.Warning,
        AssemblyMotionStatus.Submitted => StatusTone.Info,
        AssemblyMotionStatus.Admitted => StatusTone.Success,
        AssemblyMotionStatus.FormallyInsufficient => StatusTone.Danger,
        _ => StatusTone.Neutral
    };

    private string Label => AssemblyMotionDisplay.StatusLabel(I18n, Status, IsInRevision);
}
