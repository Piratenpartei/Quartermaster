using Microsoft.AspNetCore.Components;
using Quartermaster.Api;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Motions;

namespace Quartermaster.Blazor.Components;

public partial class MotionApprovalBadge {
    [Parameter, EditorRequired]
    public MotionApprovalStatus Status { get; set; }

    [Parameter]
    public string? Class { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    private StatusTone Tone => Status switch {
        MotionApprovalStatus.Pending => StatusTone.Warning,
        MotionApprovalStatus.Approved => StatusTone.Success,
        MotionApprovalStatus.Rejected => StatusTone.Danger,
        MotionApprovalStatus.FormallyRejected => StatusTone.Danger,
        _ => StatusTone.Neutral
    };

    private string Label => Status switch {
        MotionApprovalStatus.Pending => I18n[I18nKey.Ui.MotionStatus.Pending],
        MotionApprovalStatus.Approved => I18n[I18nKey.Ui.MotionStatus.Approved],
        MotionApprovalStatus.Rejected => I18n[I18nKey.Ui.MotionStatus.Rejected],
        MotionApprovalStatus.FormallyRejected => I18n[I18nKey.Ui.MotionStatus.FormallyRejected],
        MotionApprovalStatus.ClosedWithoutAction => I18n[I18nKey.Ui.MotionStatus.ClosedWithoutAction],
        _ => I18n[I18nKey.Ui.MotionStatus.Unknown]
    };
}
