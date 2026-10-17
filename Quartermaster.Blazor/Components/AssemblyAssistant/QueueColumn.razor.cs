using System;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Components.AssemblyAssistant;

public partial class QueueColumn {
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public required AssemblyQueueDTO Queue { get; set; }

    [Parameter]
    public string? SuggestedKey { get; set; }

    [Parameter]
    public bool CanControl { get; set; }

    [Parameter]
    public DateTime ServerNow { get; set; }

    [Parameter]
    public EventCallback<string> OnGiveFloor { get; set; }

    [Parameter]
    public EventCallback<(string Key, bool Skipped)> OnSkip { get; set; }

    [Parameter]
    public EventCallback<string> OnDismiss { get; set; }

    [Parameter]
    public EventCallback<string> OnUndoDismiss { get; set; }

    private static bool HasFlags(AssemblyQueueEntryDTO entry, bool suggested) {
        if (suggested || entry.MovePending || entry.AfterListClosed)
            return true;

        return entry.OrderUncertain || entry.Skipped || entry.Dismissed;
    }
}
