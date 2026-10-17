using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.AssemblyAssistant;

/// <summary>
/// OBS browser source: transparent 1920x1080 page without login that shows the current speaker, their remaining time and
/// the agenda item. It keeps retrying until the server is reachable and shows nothing while disconnected.
/// </summary>
public partial class AssemblyOverlay {
    private static readonly TimeSpan RenderInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(5);

    [Inject]
    public required AssemblyHubClient Hub { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    [Inject]
    public required LanguageService Language { get; set; }

    [Parameter]
    public Guid EventId { get; set; }

    [SupplyParameterFromQuery(Name = "lang")]
    public string? Lang { get; set; }

    [SupplyParameterFromQuery(Name = "next")]
    public bool? Next { get; set; }

    /// <summary>
    /// One of item, speaker, next, election, poll: only that element, drawn at 0,0 in its fixed size, so the OBS browser source can be
    /// exactly that size. Without it the page is the full 1920x1080 layout.
    /// </summary>
    [SupplyParameterFromQuery(Name = "element")]
    public string? Element { get; set; }

    private AssemblyOverlayDTO? Overlay;
    private TimeSpan ClockOffset;
    private bool Ready;
    private readonly CancellationTokenSource _cts = new();

    private string? element;

    protected override async Task OnInitializedAsync() {
        await Language.UseLanguageAsync(string.IsNullOrWhiteSpace(Lang) ? LanguageService.DefaultLanguage : Lang);
        element = string.IsNullOrWhiteSpace(Element) ? null : Element.Trim().ToLowerInvariant();
        Ready = true;

        Hub.OverlayChanged += OnOverlay;
        Hub.ConnectionStateChanged += OnConnectionState;
        _ = JoinLoopAsync(_cts.Token);
        _ = RenderLoopAsync(_cts.Token);
    }

    public async ValueTask DisposeAsync() {
        Hub.OverlayChanged -= OnOverlay;
        Hub.ConnectionStateChanged -= OnConnectionState;
        await _cts.CancelAsync();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }

    // The automatic reconnect only covers a connection that once stood; the first connect is retried here.
    private async Task JoinLoopAsync(CancellationToken ct) {
        while (!ct.IsCancellationRequested) {
            try {
                SetOverlay(await Hub.JoinOverlayAsync(EventId));
                await InvokeAsync(StateHasChanged);
                return;
            } catch (Exception ex) when (ex is not OperationCanceledException) {
                Console.Error.WriteLine($"Overlay: joining failed, retrying. {ex}");
            }

            try {
                await Task.Delay(RetryInterval, ct);
            } catch (OperationCanceledException) {
                return;
            }
        }
    }

    private async Task RenderLoopAsync(CancellationToken ct) {
        using var timer = new PeriodicTimer(RenderInterval);
        try {
            while (await timer.WaitForNextTickAsync(ct)) {
                if (Overlay?.Timer != null || Overlay?.Poll != null)
                    await InvokeAsync(StateHasChanged);
            }
        } catch (OperationCanceledException) {
        }
    }

    private void OnOverlay(AssemblyOverlayDTO overlay) {
        _ = InvokeAsync(() => {
            SetOverlay(overlay);
            StateHasChanged();
        });
    }

    private void OnConnectionState(bool connected) {
        if (connected)
            return;

        _ = InvokeAsync(() => {
            Overlay = null;
            StateHasChanged();
        });
    }

    private void SetOverlay(AssemblyOverlayDTO overlay) {
        Overlay = overlay;
        ClockOffset = overlay.ServerNowUtc - DateTime.UtcNow;
    }

    private bool Shows(string name) {
        if (element == null)
            return name != "next" || Next == true;

        return element == name;
    }

    private bool PollVisible() {
        var poll = Overlay?.Poll;
        if (poll == null)
            return false;

        return poll.Running || poll.VisibleUntil == null || ServerNow() <= poll.VisibleUntil.Value;
    }

    private static string PollClass(AssemblyOverlayPollDTO poll) {
        if (poll.Running || poll.Passed == null)
            return "";

        return poll.Passed.Value ? "assembly-overlay-poll-passed" : "assembly-overlay-poll-failed";
    }

    private string ContainerClass() {
        return element == null ? "assembly-overlay" : $"assembly-overlay-single assembly-overlay-single-{element}";
    }

    private DateTime ServerNow() {
        return DateTime.UtcNow + ClockOffset;
    }

    // An overrun fills the bar again, in red, so the expiry is visible at a glance.
    private string BarPercent(TimeSpan remaining) {
        var allotted = Overlay?.Timer?.AllottedSeconds;
        if (allotted == null || allotted <= 0 || remaining <= TimeSpan.Zero)
            return "100";

        var fraction = Math.Clamp(remaining.TotalSeconds / allotted.Value, 0, 1);
        return (fraction * 100).ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string TimerStateClass(TimeSpan? remaining) {
        if (remaining == null)
            return "";
        if (remaining.Value <= TimeSpan.Zero)
            return "assembly-overlay-expired";

        return remaining.Value.TotalSeconds <= 30 ? "assembly-overlay-warning" : "";
    }
}
