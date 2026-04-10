using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Events;

namespace Quartermaster.Blazor.Components;

public partial class EventStatusBadge {
    [Parameter, EditorRequired]
    public EventStatus Status { get; set; }

    private string CssClass => Status switch {
        EventStatus.Draft => "border-secondary text-secondary-emphasis",
        EventStatus.Active => "border-primary text-primary-emphasis",
        EventStatus.Completed => "border-success text-success-emphasis",
        EventStatus.Archived => "border-secondary text-body-tertiary",
        _ => "border-secondary"
    };

    private string Label => Status switch {
        EventStatus.Draft => "Entwurf",
        EventStatus.Active => "Aktiv",
        EventStatus.Completed => "Abgeschlossen",
        EventStatus.Archived => "Archiviert",
        _ => Status.ToString()
    };
}
