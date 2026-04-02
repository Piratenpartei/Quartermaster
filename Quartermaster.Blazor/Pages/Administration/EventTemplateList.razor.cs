using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Events;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class EventTemplateList {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }

    private ConfirmDialog ConfirmDialog = default!;
    private List<EventTemplateDTO>? Templates;
    private bool Loading = true;
    private bool Deleting;

    protected override async Task OnInitializedAsync() {
        await LoadTemplates();
    }

    private async Task LoadTemplates() {
        Loading = true;
        StateHasChanged();

        try {
            Templates = await Http.GetFromJsonAsync<List<EventTemplateDTO>>("/api/eventtemplates");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }

        Loading = false;
        StateHasChanged();
    }

    private async Task Delete(Guid templateId) {
        if (!await ConfirmDialog.ShowAsync("Diese Vorlage wirklich löschen?"))
            return;

        Deleting = true;
        StateHasChanged();

        try {
            await Http.DeleteAsync($"/api/eventtemplates/{templateId}");
            ToastService.Toast("Vorlage gelöscht.", "success");
            await LoadTemplates();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }

        Deleting = false;
        StateHasChanged();
    }
}
