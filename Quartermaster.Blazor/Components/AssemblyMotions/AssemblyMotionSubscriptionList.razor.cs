using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Components.AssemblyMotions;

/// <summary>The viewer's motion subscriptions with a button to unsubscribe from each.</summary>
public partial class AssemblyMotionSubscriptionList {
    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    private List<AssemblyMotionSubscriptionListItemDTO>? Items;
    private Guid? BusyMotionId;

    protected override async Task OnInitializedAsync() {
        try {
            Items = await Http.GetFromJsonAsync<List<AssemblyMotionSubscriptionListItemDTO>>("/api/users/assembly-motion-subscriptions") ?? [];
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
            Items = [];
        }
    }

    private async Task Unsubscribe(AssemblyMotionSubscriptionListItemDTO item) {
        BusyMotionId = item.MotionId;
        try {
            var response = await Http.DeleteAsync($"/api/assemblymotions/motions/{item.MotionId}/subscription");
            if (response.IsSuccessStatusCode) {
                Items?.Remove(item);
            } else {
                ToastService.ErrorKey(I18nKey.Ui.AssemblyMotionSubscription.ChangeFailed);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            BusyMotionId = null;
        }
    }
}
