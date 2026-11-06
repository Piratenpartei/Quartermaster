using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Meetings;

namespace Quartermaster.Blazor.Components;

public partial class MeetingVisibilityBadge {
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public MeetingVisibility Visibility { get; set; }

    private StatusTone Tone => Visibility == MeetingVisibility.Public ? StatusTone.Info : StatusTone.Neutral;

    private string Label => Visibility switch {
        MeetingVisibility.Public => I18n[I18nKey.Ui.MeetingVisibility.Public],
        MeetingVisibility.Private => I18n[I18nKey.Ui.MeetingVisibility.Private],
        _ => Visibility.ToString()
    };
}
