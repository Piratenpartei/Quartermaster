using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Motions;

namespace Quartermaster.Blazor.Components;

public partial class MotionApprovalBadge {
    [Parameter, EditorRequired]
    public MotionApprovalStatus Status { get; set; }

    private string CssClass => Status switch {
        MotionApprovalStatus.Pending => "border-warning text-warning-emphasis",
        MotionApprovalStatus.Approved => "border-success text-success-emphasis",
        MotionApprovalStatus.Rejected => "border-danger text-danger-emphasis",
        MotionApprovalStatus.FormallyRejected => "border-secondary text-secondary-emphasis",
        MotionApprovalStatus.ClosedWithoutAction => "border-secondary text-secondary-emphasis",
        _ => "border-secondary text-secondary-emphasis"
    };

    private string Label => Status switch {
        MotionApprovalStatus.Pending => "Ausstehend",
        MotionApprovalStatus.Approved => "Genehmigt",
        MotionApprovalStatus.Rejected => "Abgelehnt",
        MotionApprovalStatus.FormallyRejected => "Formal abgelehnt",
        MotionApprovalStatus.ClosedWithoutAction => "Ohne Beschluss",
        _ => "Unbekannt"
    };
}
