using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.DueSelector;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class DueSelectionDetail {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }

    [Parameter]
    public Guid Id { get; set; }

    private DueSelectionDetailDTO? Selection;
    private bool Loading = true;

    protected override async Task OnInitializedAsync() {
        try {
            Selection = await Http.GetFromJsonAsync<DueSelectionDetailDTO>(
                $"/api/admin/dueselections/{Id}");
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

    private static string PaymentLabel(PaymentSchedule schedule) => schedule switch {
        PaymentSchedule.Annual => "Jährlich",
        PaymentSchedule.Quarterly => "Quartalsweise",
        PaymentSchedule.Monthly => "Monatlich",
        _ => "—"
    };
}
