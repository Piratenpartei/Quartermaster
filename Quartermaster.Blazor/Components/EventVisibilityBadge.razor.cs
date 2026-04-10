using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Events;

namespace Quartermaster.Blazor.Components;

public partial class EventVisibilityBadge {
    [Parameter, EditorRequired]
    public EventVisibility Visibility { get; set; }

    private string CssClass => Visibility switch {
        EventVisibility.Public => "border-info text-info-emphasis",
        EventVisibility.MembersOnly => "border-primary text-primary-emphasis",
        EventVisibility.Private => "border-secondary text-secondary-emphasis",
        _ => "border-secondary"
    };

    private string Label => Visibility switch {
        EventVisibility.Public => "Öffentlich",
        EventVisibility.MembersOnly => "Mitglieder",
        EventVisibility.Private => "Intern",
        _ => Visibility.ToString()
    };
}
