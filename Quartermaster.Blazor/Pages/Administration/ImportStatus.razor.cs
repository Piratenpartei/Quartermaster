using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.AdministrativeDivisions;
using Quartermaster.Api.Members;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class ImportStatus {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }

    private MemberImportLogListResponse? MemberHistory;
    private AdminDivisionImportLogListResponse? AdminDivHistory;
    private bool MemberLoading = true;
    private bool AdminDivLoading = true;

    protected override async Task OnInitializedAsync() {
        await Task.WhenAll(LoadMemberHistory(), LoadAdminDivHistory());
    }

    private async Task LoadMemberHistory() {
        MemberLoading = true;
        try {
            MemberHistory = await Http.GetFromJsonAsync<MemberImportLogListResponse>(
                "/api/members/import/history?page=1&pageSize=10");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        MemberLoading = false;
    }

    private async Task LoadAdminDivHistory() {
        AdminDivLoading = true;
        try {
            AdminDivHistory = await Http.GetFromJsonAsync<AdminDivisionImportLogListResponse>(
                "/api/admindivisions/import/history?page=1&pageSize=10");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        AdminDivLoading = false;
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
