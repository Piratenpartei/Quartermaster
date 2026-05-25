using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.DueSelector;
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

    protected override async Task OnInitializedAsync() {
        try {
            App = await Http.GetFromJsonAsync<MembershipApplicationDetailDTO>(
                $"/api/admin/membershipapplications/{Id}");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }

        Loading = false;
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
