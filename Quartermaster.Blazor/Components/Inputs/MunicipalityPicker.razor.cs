using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.AdministrativeDivisions;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Components.Inputs;

/// <summary>
/// Search-by-name-or-post-code picker for German municipalities (admin divisions at depth 7).
/// Raises <see cref="OnSelect"/> with the chosen division; the parent decides what to do with it.
/// </summary>
public partial class MunicipalityPicker {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }

    /// <summary>Raised when the user picks a municipality from the results.</summary>
    [Parameter]
    public EventCallback<AdministrativeDivisionDTO> OnSelect { get; set; }

    /// <summary>Optional initial name query — runs a search on first render (e.g. the application's city).</summary>
    [Parameter]
    public string? InitialNameQuery { get; set; }

    public string NameQuery { get; private set; } = "";
    public string PostCodeQuery { get; private set; } = "";
    private List<AdministrativeDivisionDTO>? MatchingDivisions;
    private bool Searching;
    private bool HasSearched;
    private CancellationTokenSource? _searchTokenSource;

    protected override async Task OnInitializedAsync() {
        if (!string.IsNullOrWhiteSpace(InitialNameQuery)) {
            NameQuery = InitialNameQuery.Trim();
            await Search();
        }
    }

    private async Task OnNameInput(ChangeEventArgs e) {
        NameQuery = e.Value?.ToString() ?? "";
        await Search();
    }

    private async Task OnPostCodeInput(ChangeEventArgs e) {
        PostCodeQuery = e.Value?.ToString() ?? "";
        await Search();
    }

    private async Task Search() {
        // Name is the primary query; fall back to the post code.
        var query = !string.IsNullOrWhiteSpace(NameQuery) ? NameQuery.Trim() : PostCodeQuery.Trim();
        if (query.Length < 2) {
            MatchingDivisions = null;
            HasSearched = false;
            StateHasChanged();
            return;
        }

        _searchTokenSource?.Cancel();
        _searchTokenSource = new CancellationTokenSource();
        var token = _searchTokenSource.Token;

        try {
            Searching = true;
            StateHasChanged();

            var response = await Http.GetFromJsonAsync<AdministrativeDivisionSearchResponse>(
                $"/api/administrativedivisions/search?query={Uri.EscapeDataString(query)}&page=1&pageSize=50", token);

            if (response != null) {
                MatchingDivisions = response.Items.FindAll(d => d.Depth == 7);
                if (MatchingDivisions.Count == 0)
                    MatchingDivisions = response.Items;
            }

            HasSearched = true;
            Searching = false;
            StateHasChanged();
        } catch (TaskCanceledException) {
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
            Searching = false;
            StateHasChanged();
        }
    }
}
