using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.AdministrativeDivisions;

namespace Quartermaster.Blazor.Components.Inputs;

public partial class AdminDivisionPicker {
    [Inject]
    public required HttpClient Http { get; set; }

    [Parameter]
    public string Value { get; set; } = "";

    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    [Parameter]
    public string SizeClass { get; set; } = "";

    private string SearchText = "";
    private bool ShowDropdown;
    private List<AdministrativeDivisionDTO>? SearchResults;
    private CancellationTokenSource? _debounce;

    protected override async Task OnInitializedAsync() {
        if (!string.IsNullOrEmpty(Value) && Guid.TryParse(Value, out _)) {
            // Try to load the name for the pre-set value
            var response = await Http.GetFromJsonAsync<AdministrativeDivisionSearchResponse>(
                $"/api/administrativedivisions/search?query={Uri.EscapeDataString(Value)}&page=1&pageSize=1");
            if (response?.Items.Count > 0)
                SearchText = response.Items[0].Name;
        }
    }

    private async Task OnSearchInput(ChangeEventArgs e) {
        SearchText = e.Value?.ToString() ?? "";
        ShowDropdown = true;

        if (SearchText.Length < 2) {
            SearchResults = null;
            StateHasChanged();
            return;
        }

        _debounce?.Cancel();
        _debounce = new CancellationTokenSource();
        var token = _debounce.Token;

        try {
            await Task.Delay(300, token);
            var response = await Http.GetFromJsonAsync<AdministrativeDivisionSearchResponse>(
                $"/api/administrativedivisions/search?query={Uri.EscapeDataString(SearchText)}&page=1&pageSize=20");
            SearchResults = response?.Items;
            StateHasChanged();
        } catch (TaskCanceledException) { }
    }

    private async Task SelectDivision(AdministrativeDivisionDTO division) {
        Value = division.Id.ToString();
        SearchText = division.Name;
        ShowDropdown = false;
        SearchResults = null;
        await ValueChanged.InvokeAsync(Value);
    }

    private async Task ScheduleClose() {
        await Task.Delay(200);
        ShowDropdown = false;
        StateHasChanged();
    }
}
