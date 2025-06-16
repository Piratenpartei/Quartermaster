using Microsoft.AspNetCore.Components;
using Quartermaster.Blazor.Services;
using System.Diagnostics;

namespace Quartermaster.Blazor.Pages.DueSelector;

public partial class PaymentOptionSelection {
    [Inject]
    public required AppStateService AppState { get; set; }

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

    private bool DisabledPaymentSchedule(PaymentScedule paymentScedule) {
        if (EntryState == null)
            return false;

        // All enabled except for Monthly in some cases
        if (paymentScedule != PaymentScedule.Monthly)
            return false;

        // Always 12€ => Cannot pay monthly
        if (EntryState.SelectedValuation == SelectedValuation.Underage)
            return true;
        if (EntryState.SelectedValuation == SelectedValuation.Reduced && EntryState.ReducedAmount < 36)
            return true;

        return false;
    }

    private static string TextForPaymentSchedule(PaymentScedule paymentScedule) {
        return paymentScedule switch {
            PaymentScedule.None => "",
            PaymentScedule.Annual => "Jährlich (fällig zum 01.01 eines Jahres)",
            PaymentScedule.Quarterly => "Quartalsweise (fällig zum 01.01, 01.04, 01.07 und 01.10)",
            PaymentScedule.Monthly => "Monatlich - ab 36€ Jahresbeitrag (fällig zum ersten eines Monats)",
            _ => throw new UnreachableException()
        };
    }

    private static bool ExcludedPaymentSchedule(PaymentScedule paymentScedule)
        => paymentScedule == PaymentScedule.None;
}