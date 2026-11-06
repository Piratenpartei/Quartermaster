using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Meetings;

namespace Quartermaster.Blazor.Components;

public partial class MeetingStatusBadge {
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public MeetingStatus Status { get; set; }

    private StatusTone Tone => Status switch {
        MeetingStatus.Scheduled => StatusTone.Info,
        MeetingStatus.InProgress => StatusTone.Warning,
        MeetingStatus.Completed => StatusTone.Success,
        _ => StatusTone.Neutral
    };

    private string Label => Status switch {
        MeetingStatus.Draft => I18n[I18nKey.Ui.MeetingStatus.Draft],
        MeetingStatus.Scheduled => I18n[I18nKey.Ui.MeetingStatus.Scheduled],
        MeetingStatus.InProgress => I18n[I18nKey.Ui.MeetingStatus.InProgress],
        MeetingStatus.Completed => I18n[I18nKey.Ui.MeetingStatus.Completed],
        MeetingStatus.Archived => I18n[I18nKey.Ui.MeetingStatus.Archived],
        _ => Status.ToString()
    };
}
