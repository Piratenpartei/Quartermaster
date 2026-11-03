using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Components.AssemblyMotions;

/// <summary>Subscribe button of a motion; the state comes from the motion detail, which <see cref="OnChanged"/> reloads.</summary>
public partial class AssemblyMotionSubscriptionToggle {
    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter]
    public Guid MotionId { get; set; }

    [Parameter]
    public bool IsSubscribed { get; set; }

    [Parameter]
    public bool CanSubscribe { get; set; }

    [Parameter]
    public EventCallback OnChanged { get; set; }

    private bool Busy;

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
                await ToastService.ErrorAsync(response, ToastService.Translate(I18nKey.Ui.AssemblyMotionSubscription.ChangeFailed));
            await OnChanged.InvokeAsync();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Busy = false;
        }
    }

    private string Url => $"/api/assemblymotions/motions/{MotionId}/subscription";
}
