using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Events;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Components.Events;

/// <summary>Subscribe button of an event; renders nothing for anonymous visitors or events the viewer can't see.</summary>
public partial class EventSubscriptionToggle {
    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    [Inject]
    public required AuthService AuthService { get; set; }

    [Parameter]
    public Guid EventId { get; set; }

    private EventSubscriptionStateDTO? State;
    private bool Busy;
    private Guid _loadedEventId;

    protected override async Task OnParametersSetAsync() {
        if (EventId == _loadedEventId)
            return;
        _loadedEventId = EventId;
        await LoadState();
    }

    private async Task LoadState() {
        await AuthService.WaitForInitializationAsync();
        if (!AuthService.IsAuthenticated) {
            State = null;
            return;
        }

        try {
            var response = await Http.GetAsync(Url);
            State = response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<EventSubscriptionStateDTO>()
                : null;
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
            State = null;
        }
    }

    private async Task Subscribe() {
        await Change(() => Http.PostAsJsonAsync(Url, new { }));
    }

    private async Task Unsubscribe() {
        await Change(() => Http.DeleteAsync(Url));
    }

    private async Task Change(Func<Task<HttpResponseMessage>> send) {
        Busy = true;
        try {
            var response = await send();
            if (!response.IsSuccessStatusCode)
                ToastService.ErrorKey(I18nKey.Ui.EventSubscription.ChangeFailed);
            await LoadState();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Busy = false;
        }
    }

    private string Url => $"/api/events/{EventId}/subscription";
}
