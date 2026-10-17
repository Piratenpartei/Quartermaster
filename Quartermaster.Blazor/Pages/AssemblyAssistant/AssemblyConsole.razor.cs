using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.AssemblyAssistant;

public partial class AssemblyConsole {
    private static readonly TimeSpan RenderInterval = TimeSpan.FromMilliseconds(250);

    [Inject]
    public required AssemblyHubClient Hub { get; set; }

    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    [Inject]
    public required IJSRuntime Js { get; set; }

    [Inject]
    public required AuthService Auth { get; set; }

    [Parameter]
    public Guid EventId { get; set; }

    private AssemblyConsoleJoinDTO? Join;
    private AssemblySnapshotDTO? Snapshot;
    private TimeSpan ClockOffset;
    private ConfirmDialog? EndDialog;
    private bool Busy;
    private string ItemInput = "";
    private int? MotionMinutesInput;
    private TimeOnly? CloseAtInput;
    private readonly CancellationTokenSource _renderCts = new();
    private DotNetObjectReference<AssemblyConsole>? shortcutTarget;
    private IJSObjectReference? shortcutHandle;

    protected override async Task OnInitializedAsync() {
        Hub.SnapshotChanged += OnSnapshot;
        Hub.SessionEnded += OnSessionEnded;
        Hub.ConsoleRejoined += OnRejoined;

        try {
            ApplyJoin(await Hub.JoinConsoleAsync(EventId));
        } catch (Exception ex) {
            ToastService.Error(ex);
            Join = new AssemblyConsoleJoinDTO();
        }

        _ = RenderLoopAsync(_renderCts.Token);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) {
        if (shortcutHandle != null || Join?.CanControl != true)
            return;

        shortcutTarget = DotNetObjectReference.Create(this);
        shortcutHandle = await Js.InvokeAsync<IJSObjectReference>("qmAssemblyShortcuts.register", shortcutTarget);
    }

    [JSInvokable]
    public async Task OnShortcut(string action) {
        if (Snapshot == null)
            return;

        var current = Snapshot.Floor.LastOrDefault(f => f.Channel == ChannelRole.Speaker && !f.DonePending)
            ?? Snapshot.Floor.LastOrDefault(f => !f.DonePending);
        switch (action) {
            case "give_floor":
                if (Snapshot.SuggestedKey != null)
                    await Run("GiveFloor", Snapshot.SuggestedKey);
                break;
            case "done":
                if (current != null)
                    await Run("Done", current.Session);
                break;
            case "add_minute":
                if (current != null && current.Timer.AllottedSeconds != null)
                    await Run("AddMinute", current.Session);
                break;
        }
    }

    public async ValueTask DisposeAsync() {
        Hub.SnapshotChanged -= OnSnapshot;
        Hub.SessionEnded -= OnSessionEnded;
        Hub.ConsoleRejoined -= OnRejoined;
        await _renderCts.CancelAsync();
        _renderCts.Dispose();

        if (shortcutHandle != null) {
            try {
                await shortcutHandle.InvokeVoidAsync("dispose");
                await shortcutHandle.DisposeAsync();
            } catch (JSDisconnectedException) {
            }
        }
        shortcutTarget?.Dispose();
        GC.SuppressFinalize(this);
    }

    private void ApplyJoin(AssemblyConsoleJoinDTO join) {
        Join = join;
        SetSnapshot(join.Snapshot);
    }

    private void SetSnapshot(AssemblySnapshotDTO? snapshot) {
        Snapshot = snapshot;
        if (snapshot != null)
            ClockOffset = snapshot.ServerNowUtc - DateTime.UtcNow;
    }

    private void OnSnapshot(AssemblySnapshotDTO snapshot) {
        if (snapshot.EventId != EventId)
            return;

        _ = InvokeAsync(() => {
            SetSnapshot(snapshot);
            StateHasChanged();
        });
    }

    private void OnSessionEnded() {
        _ = InvokeAsync(() => {
            Snapshot = null;
            StateHasChanged();
        });
    }

    private void OnRejoined(AssemblyConsoleJoinDTO join) {
        _ = InvokeAsync(() => {
            ApplyJoin(join);
            StateHasChanged();
        });
    }

    // Timers and waiting times count down locally between pushes.
    private async Task RenderLoopAsync(CancellationToken ct) {
        using var timer = new PeriodicTimer(RenderInterval);
        try {
            while (await timer.WaitForNextTickAsync(ct)) {
                if (Snapshot != null)
                    await InvokeAsync(StateHasChanged);
            }
        } catch (OperationCanceledException) {
        }
    }

    private DateTime ServerNow() {
        return DateTime.UtcNow + ClockOffset;
    }

    private string ListStatusText() {
        if (Snapshot?.ListClosedAt != null)
            return I18n[I18nParams.With(I18nKey.Ui.AssemblyConsole.ListClosed, ("time", BerlinTime.FromUtc(Snapshot.ListClosedAt.Value).ToString("HH:mm")))];
        if (Snapshot?.ListClosesAt != null)
            return I18n[I18nParams.With(I18nKey.Ui.AssemblyConsole.ListClosesAt, ("time", BerlinTime.FromUtc(Snapshot.ListClosesAt.Value).ToString("HH:mm")))];

        return I18n[I18nKey.Ui.AssemblyConsole.ListOpen];
    }

    private string MotionMinutesText() {
        if (Snapshot?.MotionSpeechMinutes == null)
            return I18n[I18nKey.Ui.AssemblyConsole.MotionMinutesDefault];

        return I18n[I18nParams.With(I18nKey.Ui.AssemblyConsole.MinutesValue, ("minutes", Snapshot.MotionSpeechMinutes.Value.ToString()))];
    }

    private async Task Run(string method, params object?[] arguments) {
        if (Snapshot == null)
            return;

        try {
            var result = await Hub.CommandAsync(method, [EventId, Snapshot.Version, .. arguments]);
            if (!result.Accepted)
                ToastService.ErrorKey(result.MessageKey ?? I18nKey.Ui.AssemblyConsole.Reject.Stale);
        } catch (Exception ex) {
            ToastService.Error(ex);
        }
    }

    private Task PresentCandidate(int index) {
        return Run("SetCurrentCandidate", (int?)index);
    }

    private async Task CreateProceduralTopics() {
        try {
            var result = await Hub.CommandAsync("CreateProceduralTopics", EventId);
            if (!result.Accepted)
                ToastService.ErrorKey(result.MessageKey ?? I18nKey.Ui.AssemblyConsole.Reject.Stale);
        } catch (Exception ex) {
            ToastService.Error(ex);
        }
    }

    private async Task SelectAgendaItem(string? value) {
        if (int.TryParse(value, out var agendaItemId))
            await Run("SelectAgendaItem", agendaItemId);
    }

    private async Task ChangePollSettings(AssemblyPollSettingsDTO settings) {
        try {
            var result = await Hub.CommandAsync("SetPollSettings", EventId, settings.DurationSeconds, settings.AutoStart, settings.AutoStop, settings.AutoPublish,
                settings.Project);
            if (!result.Accepted)
                ToastService.ErrorKey(result.MessageKey ?? I18nKey.Ui.AssemblyConsole.Reject.Stale);
        } catch (Exception ex) {
            ToastService.Error(ex);
        }
    }

    private async Task SetItem() {
        await Run("SetItem", ItemInput);
        ItemInput = "";
    }

    private async Task ApplyMotionMinutes() {
        if (MotionMinutesInput == null)
            return;

        await Run("SetMotionSpeechMinutes", MotionMinutesInput);
        MotionMinutesInput = null;
    }

    private Task ClearMotionMinutes() {
        return Run("SetMotionSpeechMinutes", (int?)null);
    }

    private Task CloseNow() {
        return Run("CloseList", (DateTime?)null);
    }

    private async Task CloseAt() {
        if (CloseAtInput == null)
            return;

        var berlinToday = BerlinTime.FromUtc(ServerNow()).Date;
        var closesAt = BerlinTime.ToUtc(berlinToday + CloseAtInput.Value.ToTimeSpan());
        await Run("CloseList", (DateTime?)closesAt);
        CloseAtInput = null;
    }

    private Task Reopen() {
        return Run("ReopenList");
    }

    private async Task StartSession() {
        await SessionRequest("start");
    }

    private async Task EndSession() {
        if (EndDialog == null || !await EndDialog.ShowAsync())
            return;

        await SessionRequest("end");
    }

    private async Task SessionRequest(string action) {
        Busy = true;
        try {
            var response = await Http.PostAsJsonAsync($"/api/assembly/{EventId}/session/{action}", new { });
            if (response.IsSuccessStatusCode)
                return;

            var result = await response.Content.ReadFromJsonAsync<AssemblyCommandResult>();
            if (result?.MessageKey != null) {
                ToastService.ErrorKey(result.MessageKey);
            } else {
                await ToastService.ErrorAsync(response);
            }
        } catch (Exception ex) {
            ToastService.Error(ex);
        } finally {
            Busy = false;
        }
    }
}
