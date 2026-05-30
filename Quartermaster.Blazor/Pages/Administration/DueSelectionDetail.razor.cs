using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class DueSelectionDetail {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }

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

    private string ValuationLabel(SelectedValuation valuation) => valuation switch {
        SelectedValuation.MonthlyPayGroup => I18n[I18nKey.Ui.DueSelectionDetail.ValuationMonthlyPay],
        SelectedValuation.OnePercentYearlyPay => I18n[I18nKey.Ui.DueSelectionDetail.ValuationOnePercent],
        SelectedValuation.Underage => I18n[I18nKey.Ui.DueSelectionDetail.ValuationUnderage],
        SelectedValuation.Reduced => I18n[I18nKey.Ui.DueSelectionDetail.ValuationReduced],
        _ => I18n[I18nKey.Ui.DueSelectionDetail.ValuationUnknown]
    };

    private string PaymentLabel(PaymentSchedule schedule) => schedule switch {
        PaymentSchedule.Annual => I18n[I18nKey.Ui.DueSelectionDetail.ScheduleAnnual],
        PaymentSchedule.Quarterly => I18n[I18nKey.Ui.DueSelectionDetail.ScheduleQuarterly],
        PaymentSchedule.Monthly => I18n[I18nKey.Ui.DueSelectionDetail.ScheduleMonthly],
        _ => I18n[I18nKey.Ui.DueSelectionDetail.ScheduleNone]
    };
}
