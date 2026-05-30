using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Events;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Components;

public partial class EventVisibilityBadge {
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public EventVisibility Visibility { get; set; }

    private string CssClass => Visibility switch {
        EventVisibility.Public => "border-info text-info-emphasis",
        EventVisibility.MembersOnly => "border-primary text-primary-emphasis",
        EventVisibility.Private => "border-secondary text-secondary-emphasis",
        _ => "border-secondary"
    };

    private string Label => Visibility switch {
        EventVisibility.Public => I18n[I18nKey.Ui.EventVisibility.Public],
        EventVisibility.MembersOnly => I18n[I18nKey.Ui.EventVisibility.MembersOnly],
        EventVisibility.Private => I18n[I18nKey.Ui.EventVisibility.Private],
        _ => Visibility.ToString()
    };
}
