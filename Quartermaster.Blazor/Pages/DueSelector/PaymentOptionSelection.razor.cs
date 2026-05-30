using System.Diagnostics;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.DueSelector;

public partial class PaymentOptionSelection {
    [Inject]
    public required AppStateService AppState { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter]
    public required string ReturnUrl { get; set; }

    private DueSelectorEntryState? EntryState;

    protected override void OnInitialized() {
        EntryState = AppState.GetEntryState<DueSelectorEntryState>();
    }

    private void UseNameAsAccountHolder() {
        if (EntryState == null)
            return;

        EntryState.AccountHolder = EntryState.FirstName + " " + EntryState.LastName;
    }

    private bool DisabledPaymentSchedule(PaymentSchedule paymentScedule) {
        if (EntryState == null)
            return false;

        if (paymentScedule != PaymentSchedule.Monthly)
            return false;

        if (EntryState.SelectedValuation == SelectedValuation.Underage)
            return true;
        if (EntryState.SelectedValuation == SelectedValuation.Reduced && EntryState.ReducedAmount < 36)
            return true;

        return false;
    }

    private string TextForPaymentSchedule(PaymentSchedule paymentScedule) {
        return paymentScedule switch {
            PaymentSchedule.None => "",
            PaymentSchedule.Annual => I18n[I18nKey.Ui.PaymentOptionSelection.ScheduleAnnual],
            PaymentSchedule.Quarterly => I18n[I18nKey.Ui.PaymentOptionSelection.ScheduleQuarterly],
            PaymentSchedule.Monthly => I18n[I18nKey.Ui.PaymentOptionSelection.ScheduleMonthly],
            _ => throw new UnreachableException()
        };
    }

    private static bool ExcludedPaymentSchedule(PaymentSchedule paymentScedule)
        => paymentScedule == PaymentSchedule.None;
}
