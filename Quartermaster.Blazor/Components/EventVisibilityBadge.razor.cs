using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Events;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Components;

public partial class EventVisibilityBadge {
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public EventVisibility Visibility { get; set; }

    private StatusTone Tone => Visibility == EventVisibility.Private ? StatusTone.Neutral : StatusTone.Info;

    private string Label => Visibility switch {
        EventVisibility.Public => I18n[I18nKey.Ui.EventVisibility.Public],
        EventVisibility.MembersOnly => I18n[I18nKey.Ui.EventVisibility.MembersOnly],
        EventVisibility.Private => I18n[I18nKey.Ui.EventVisibility.Private],
        _ => Visibility.ToString()
    };
}
