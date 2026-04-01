using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Members;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class MemberImportHistory {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }

    private MemberImportLogListResponse? Response;
    private MemberImportLogDTO? ImportResult;
    private bool Loading = true;
    private bool Importing;
    private int CurrentPage = 1;
    private const int PageSize = 25;
    private Guid? ExpandedLogId;

    private int TotalPages => Response == null ? 0
        : (int)Math.Ceiling((double)Response.TotalCount / PageSize);

    protected override async Task OnInitializedAsync() {
        await LoadHistory();
    }

    private async Task LoadHistory() {
        Loading = true;
        StateHasChanged();

        try {
            Response = await Http.GetFromJsonAsync<MemberImportLogListResponse>(
                $"/api/members/import/history?page={CurrentPage}&pageSize={PageSize}");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }

        Loading = false;
        StateHasChanged();
    }

    private async Task GoToPage(int selectedPage) {
        CurrentPage = selectedPage;
        await LoadHistory();
    }

    private async Task TriggerImport() {
        Importing = true;
        StateHasChanged();

        try {
            var response = await Http.PostAsync("/api/members/import", null);
            if (response.IsSuccessStatusCode) {
                ImportResult = await response.Content.ReadFromJsonAsync<MemberImportLogDTO>();
                CurrentPage = 1;
                await LoadHistory();
            } else {
                ToastService.Error(details: $"HTTP {(int)response.StatusCode}");
            }
        } catch (Exception ex) {
            ToastService.Error(ex);
            await LoadHistory();
        }

        Importing = false;
        StateHasChanged();
    }

    private void ToggleErrors(Guid logId) {
        ExpandedLogId = ExpandedLogId == logId ? null : logId;
    }

    private static List<string> ParseErrors(string errorsJson) {
        try {
            return JsonSerializer.Deserialize<List<string>>(errorsJson) ?? new();
        } catch {
            return new List<string> { errorsJson };
        }
    }

    private static string FormatDuration(long ms) {
        if (ms < 1000)
            return $"{ms}ms";
        return $"{ms / 1000.0:F1}s";
    }
}
