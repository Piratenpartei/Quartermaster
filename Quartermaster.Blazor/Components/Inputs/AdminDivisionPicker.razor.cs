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
    private const int MaxResults = 20;

    [Inject]
    public required HttpClient Http { get; set; }

    [Parameter]
    public string Value { get; set; } = "";

    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    /// <summary>Fires alongside <see cref="ValueChanged"/> but with the full selected DTO — needed by callers (multi-pickers) that have to remember the display name.</summary>
    [Parameter]
    public EventCallback<AdministrativeDivisionDTO> OnSelect { get; set; }

    /// <summary>When true, the picker resets to empty after each selection so a wrapper can collect multiple picks.</summary>
    [Parameter]
    public bool AutoClearAfterSelect { get; set; }

    [Parameter]
    public string SizeClass { get; set; } = "";

    /// <summary>Name to display for a pre-set <see cref="Value"/> (search can't resolve a raw id).</summary>
    [Parameter]
    public string InitialDisplayText { get; set; } = "";

    /// <summary>Set when the picker sits inside a surrounding input group.</summary>
    [Parameter]
    public bool InInputGroup { get; set; }

    private string SearchText = "";
    private List<AdministrativeDivisionDTO>? SearchResults;
    private CancellationTokenSource? _debounce;

    protected override void OnInitialized() {
        if (!string.IsNullOrEmpty(Value) && !string.IsNullOrEmpty(InitialDisplayText)) {
            SearchText = InitialDisplayText;
        }
    }

    private async Task OnSearchInput(string text) {
        SearchText = text;

        if (SearchText.Length < 2) {
            SearchResults = null;
            return;
        }

        _debounce?.Cancel();
        _debounce = new CancellationTokenSource();
        var token = _debounce.Token;

        try {
            await Task.Delay(300, token);
            var response = await Http.GetFromJsonAsync<AdministrativeDivisionSearchResponse>(
                $"/api/administrativedivisions/search?query={Uri.EscapeDataString(SearchText)}&page=1&pageSize={MaxResults}");
            SearchResults = response?.Items;
            StateHasChanged();
        } catch (TaskCanceledException) { }
    }

    private async Task SelectDivision(AdministrativeDivisionDTO division) {
        Value = division.Id.ToString();
        SearchText = division.Name;
        SearchResults = null;
        await ValueChanged.InvokeAsync(Value);
        await OnSelect.InvokeAsync(division);
        if (AutoClearAfterSelect) {
            Value = "";
            SearchText = "";
            await ValueChanged.InvokeAsync("");
        }
    }
}
