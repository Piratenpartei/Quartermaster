using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Meetings;

namespace Quartermaster.Blazor.Components;

public partial class MeetingStatusBadge {
    [Parameter, EditorRequired]
    public MeetingStatus Status { get; set; }

    private string CssClass => Status switch {
        MeetingStatus.Draft => "border-secondary text-secondary-emphasis",
        MeetingStatus.Scheduled => "border-primary text-primary-emphasis",
        MeetingStatus.InProgress => "border-warning text-warning-emphasis",
        MeetingStatus.Completed => "border-success text-success-emphasis",
        MeetingStatus.Archived => "border-secondary text-body-tertiary",
        _ => "border-secondary"
    };

    private string Label => Status switch {
        MeetingStatus.Draft => "Entwurf",
        MeetingStatus.Scheduled => "Geplant",
        MeetingStatus.InProgress => "Laufend",
        MeetingStatus.Completed => "Abgeschlossen",
        MeetingStatus.Archived => "Archiviert",
        _ => Status.ToString()
    };
}
