using Microsoft.AspNetCore.Components;

namespace Quartermaster.Blazor.Components;

public partial class MotionApprovalBadge {
    /// <summary>
    /// Approval status as int (0=Pending, 1=Approved, 2=Rejected, 3=FormallyRejected, 4=ClosedWithoutDecision).
    /// Stored as int to match the wire format used in MotionDTO.
    /// </summary>
    [Parameter, EditorRequired]
    public int Status { get; set; }

    private string CssClass => Status switch {
        0 => "border-warning text-warning-emphasis",
        1 => "border-success text-success-emphasis",
        2 => "border-danger text-danger-emphasis",
        3 => "border-secondary text-secondary-emphasis",
        4 => "border-secondary text-secondary-emphasis",
        _ => "border-secondary text-secondary-emphasis"
    };

    private string Label => Status switch {
        0 => "Ausstehend",
        1 => "Genehmigt",
        2 => "Abgelehnt",
        3 => "Formal abgelehnt",
        4 => "Ohne Beschluss",
        _ => "Unbekannt"
    };
}
