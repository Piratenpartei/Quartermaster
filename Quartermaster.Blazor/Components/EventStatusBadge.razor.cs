using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Events;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Components;

public partial class EventStatusBadge {
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public EventStatus Status { get; set; }

    private StatusTone Tone => Status switch {
        EventStatus.Active => StatusTone.Info,
        EventStatus.Completed => StatusTone.Success,
        _ => StatusTone.Neutral
    };

    private string Label => Status switch {
        EventStatus.Draft => I18n[I18nKey.Ui.Label.EventStatusDraft],
        EventStatus.Active => I18n[I18nKey.Ui.Label.EventStatusActive],
        EventStatus.Completed => I18n[I18nKey.Ui.Label.EventStatusCompleted],
        EventStatus.Archived => I18n[I18nKey.Ui.Label.EventStatusArchived],
        _ => Status.ToString()
    };
}
