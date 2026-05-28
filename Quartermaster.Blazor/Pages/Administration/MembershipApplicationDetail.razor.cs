using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.I18n;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class MembershipApplicationDetail {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }

    [Parameter]
    public Guid Id { get; set; }

    private MembershipApplicationDetailDTO? App;
    private bool Loading = true;
    private int? WelcomeMemberNumber;
    private bool Sending;

    private static readonly List<string> WelcomePermissions = new() { PermissionIdentifier.ProcessApplications };

    protected override async Task OnInitializedAsync() {
        await LoadAsync();
        Loading = false;
    }

    private async Task LoadAsync() {
        try {
            App = await Http.GetFromJsonAsync<MembershipApplicationDetailDTO>(
                $"/api/admin/membershipapplications/{Id}");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task SendWelcome() {
        if (Sending || App == null || WelcomeMemberNumber == null || WelcomeMemberNumber <= 0) {
            return;
        }
        Sending = true;
        StateHasChanged();
        try {
            var resp = await Http.PostAsJsonAsync("/api/admin/membershipapplications/welcome",
                new { Id = App.Id, MemberNumber = WelcomeMemberNumber.Value });
            if (resp.IsSuccessStatusCode) {
                ToastService.ToastKey(I18nKey.Ui.Toast.WelcomeMailSent);
                await LoadAsync();
            } else {
                await ToastService.ErrorAsync(resp);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Sending = false;
            StateHasChanged();
        }
    }

    private static string ValuationLabel(SelectedValuation valuation) => valuation switch {
        SelectedValuation.MonthlyPayGroup => "Monatseinkommen",
        SelectedValuation.OnePercentYearlyPay => "1% Jahreseinkommen",
        SelectedValuation.Underage => "Minderjährig (12€)",
        SelectedValuation.Reduced => "Geminderter Beitrag",
        _ => "Unbekannt"
    };

    private static string DueStatusLabel(DueSelectionStatus status) => status switch {
        DueSelectionStatus.Pending => "Ausstehend",
        DueSelectionStatus.Approved => "Genehmigt",
        DueSelectionStatus.Rejected => "Abgelehnt",
        DueSelectionStatus.AutoApproved => "Automatisch genehmigt",
        _ => "Unbekannt"
    };
}
