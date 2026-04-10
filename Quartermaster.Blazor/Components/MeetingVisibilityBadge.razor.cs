using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Meetings;

namespace Quartermaster.Blazor.Components;

public partial class MeetingVisibilityBadge {
    [Parameter, EditorRequired]
    public MeetingVisibility Visibility { get; set; }

    private string CssClass => Visibility switch {
        MeetingVisibility.Public => "border-info text-info-emphasis",
        MeetingVisibility.Private => "border-secondary text-secondary-emphasis",
        _ => "border-secondary"
    };

    private string Label => Visibility switch {
        MeetingVisibility.Public => "Öffentlich",
        MeetingVisibility.Private => "Privat",
        _ => Visibility.ToString()
    };
}
