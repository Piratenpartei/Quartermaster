using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.AdministrativeDivisions;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class AdministrativeDivisionSearch {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }

    private string SearchQuery { get; set; } = "";
    private int CurrentPage = 1;
    private const int PageSize = 25;
    private bool Loading;
    private AdministrativeDivisionSearchResponse? Response;
    private CancellationTokenSource? _debounceTokenSource;

    private int TotalPages => Response == null ? 0 : (int)Math.Ceiling((double)Response.TotalCount / PageSize);

    protected override async Task OnInitializedAsync() {
        await Search();
    }

    private async Task OnSearchKeyUp() {
        _debounceTokenSource?.Cancel();
        _debounceTokenSource = new CancellationTokenSource();
        var token = _debounceTokenSource.Token;

        try {
            await Task.Delay(300, token);
            CurrentPage = 1;
            await Search();
        } catch (TaskCanceledException) { }
    }

    private async Task GoToPage(int page) {
        if (page < 1 || page > TotalPages)
            return;

        CurrentPage = page;
        await Search();
    }

    private async Task Search() {
        Loading = true;
        StateHasChanged();

        try {
            var query = string.IsNullOrWhiteSpace(SearchQuery) ? "" : SearchQuery;
            Response = await Http.GetFromJsonAsync<AdministrativeDivisionSearchResponse>(
                $"/api/administrativedivisions/search?query={Uri.EscapeDataString(query)}&page={CurrentPage}&pageSize={PageSize}");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }

        Loading = false;
        StateHasChanged();
    }

    private string DepthLabel(int depth) => depth switch {
        1 => I18n[I18nKey.Ui.AdminDivisionSearch.DepthWorld],
        3 => I18n[I18nKey.Ui.AdminDivisionSearch.DepthCountry],
        4 => I18n[I18nKey.Ui.AdminDivisionSearch.DepthFederalState],
        5 => I18n[I18nKey.Ui.AdminDivisionSearch.DepthGovernmentRegion],
        6 => I18n[I18nKey.Ui.AdminDivisionSearch.DepthDistrict],
        7 => I18n[I18nKey.Ui.AdminDivisionSearch.DepthMunicipality],
        8 => I18n[I18nKey.Ui.AdminDivisionSearch.DepthLocality],
        _ => depth.ToString()
    };
}
