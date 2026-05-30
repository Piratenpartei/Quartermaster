using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Meetings;

namespace Quartermaster.Blazor.Components;

public partial class MeetingVisibilityBadge {
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public MeetingVisibility Visibility { get; set; }

    private string CssClass => Visibility switch {
        MeetingVisibility.Public => "border-info text-info-emphasis",
        MeetingVisibility.Private => "border-secondary text-secondary-emphasis",
        _ => "border-secondary"
    };

    private string Label => Visibility switch {
        MeetingVisibility.Public => I18n[I18nKey.Ui.MeetingVisibility.Public],
        MeetingVisibility.Private => I18n[I18nKey.Ui.MeetingVisibility.Private],
        _ => Visibility.ToString()
    };
}
