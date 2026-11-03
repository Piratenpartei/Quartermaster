using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Events;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Components.Events;

/// <summary>The viewer's event subscriptions with a button to unsubscribe from each.</summary>
public partial class EventSubscriptionList {
    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    private List<EventSubscriptionListItemDTO>? Items;
    private Guid? BusyEventId;

    protected override async Task OnInitializedAsync() {
        try {
            Items = await Http.GetFromJsonAsync<List<EventSubscriptionListItemDTO>>("/api/users/event-subscriptions") ?? [];
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
            Items = [];
        }
    }

    private async Task Unsubscribe(EventSubscriptionListItemDTO item) {
        BusyEventId = item.EventId;
        try {
            var response = await Http.DeleteAsync($"/api/events/{item.EventId}/subscription");
            if (response.IsSuccessStatusCode) {
                Items?.Remove(item);
            } else {
                ToastService.ErrorKey(I18nKey.Ui.EventSubscription.ChangeFailed);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            BusyEventId = null;
        }
    }
}
