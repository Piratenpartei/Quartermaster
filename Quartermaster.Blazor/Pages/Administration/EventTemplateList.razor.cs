using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Events;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class EventTemplateList {
    [Inject]
    public required HttpClient Http { get; set; }

    private List<EventTemplateDTO>? Templates;
    private bool Loading = true;
    private bool Deleting;

    protected override async Task OnInitializedAsync() {
        await LoadTemplates();
    }

    private async Task LoadTemplates() {
        Loading = true;
        StateHasChanged();

        Templates = await Http.GetFromJsonAsync<List<EventTemplateDTO>>("/api/eventtemplates");

        Loading = false;
        StateHasChanged();
    }

    private async Task Delete(Guid templateId) {
        Deleting = true;
        StateHasChanged();

        await Http.DeleteAsync($"/api/eventtemplates/{templateId}");
        await LoadTemplates();

        Deleting = false;
        StateHasChanged();
    }
}
