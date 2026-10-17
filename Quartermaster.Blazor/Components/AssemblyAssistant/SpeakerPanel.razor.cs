using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Components.AssemblyAssistant;

public partial class SpeakerPanel {
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public required List<AssemblyFloorDTO> Floor { get; set; }

    [Parameter]
    public List<uint> SpeakingSessions { get; set; } = [];

    [Parameter]
    public bool CanControl { get; set; }

    [Parameter]
    public DateTime ServerNow { get; set; }

    [Parameter]
    public EventCallback<uint> OnDone { get; set; }

    [Parameter]
    public EventCallback<uint> OnAddMinute { get; set; }

    [Parameter]
    public EventCallback<uint> OnPause { get; set; }

    [Parameter]
    public EventCallback<uint> OnResume { get; set; }

    [Parameter]
    public EventCallback<uint> OnStartNow { get; set; }

    [Parameter]
    public EventCallback<(uint Session, int Seconds)> OnSetTime { get; set; }

    private readonly Dictionary<uint, int?> TimeInputs = new();

    protected override void OnParametersSet() {
        foreach (var holder in Floor)
            TimeInputs.TryAdd(holder.Session, null);
    }

    private string StatusText(AssemblyFloorDTO holder) {
        if (holder.DonePending)
            return I18n[I18nKey.Ui.AssemblyConsole.DonePending];
        if (holder.Timer.AllottedSeconds == null)
            return I18n[I18nKey.Ui.AssemblyConsole.Untimed];
        if (holder.Timer.Paused)
            return I18n[I18nKey.Ui.AssemblyConsole.Paused];
        if (holder.Timer.StartedAt == null)
            return I18n[I18nKey.Ui.AssemblyConsole.WaitingForVoice];

        return "\u00A0";
    }

    private async Task SetTime(uint session) {
        if (!TimeInputs.TryGetValue(session, out var seconds) || seconds == null)
            return;

        await OnSetTime.InvokeAsync((session, seconds.Value));
        TimeInputs[session] = null;
    }
}
