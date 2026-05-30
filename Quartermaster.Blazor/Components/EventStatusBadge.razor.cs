using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Events;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Components;

public partial class EventStatusBadge {
    [Inject]
    public required I18nService I18n { get; set; }

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
        EventStatus.Draft => I18n[I18nKey.Ui.Label.EventStatusDraft],
        EventStatus.Active => I18n[I18nKey.Ui.Label.EventStatusActive],
        EventStatus.Completed => I18n[I18nKey.Ui.Label.EventStatusCompleted],
        EventStatus.Archived => I18n[I18nKey.Ui.Label.EventStatusArchived],
        _ => Status.ToString()
    };
}
