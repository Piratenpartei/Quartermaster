using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.AuditLog;
using Quartermaster.Api.Members;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class MemberDetail {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }

    [Parameter]
    public Guid Id { get; set; }

    private MemberDetailDTO? Member;
    private bool Loading = true;
    private List<AuditLogDTO>? AuditLogs;
    private string NewAdminDivId = "";
    private bool SavingAdminDiv;

    protected override async Task OnInitializedAsync() {
        try {
            Member = await Http.GetFromJsonAsync<MemberDetailDTO>($"/api/members/{Id}");
            AuditLogs = await Http.GetFromJsonAsync<List<AuditLogDTO>>($"/api/auditlog?entityType=Member&entityId={Id}");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }

        Loading = false;
    }

    private void OnAdminDivChanged(string value) {
        NewAdminDivId = value;
    }

    private async Task SaveAdminDivision() {
        if (string.IsNullOrEmpty(NewAdminDivId) || !Guid.TryParse(NewAdminDivId, out var divId))
            return;

        SavingAdminDiv = true;
        StateHasChanged();

        try {
            var response = await Http.PutAsJsonAsync($"/api/members/{Id}/admindivision",
                new { ResidenceAdministrativeDivisionId = divId });

            if (response.IsSuccessStatusCode) {
                ToastService.Toast("Verwaltungsbezirk zugewiesen.", "success");
                Member = await Http.GetFromJsonAsync<MemberDetailDTO>($"/api/members/{Id}");
                NewAdminDivId = "";
            } else {
                ToastService.Error(details: $"HTTP {(int)response.StatusCode}");
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }

        SavingAdminDiv = false;
        StateHasChanged();
    }
}
