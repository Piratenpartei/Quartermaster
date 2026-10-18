using System.Diagnostics;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Banking;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Pages.DueSelector;

/// <summary>The payment step both the membership application and the due selection use.</summary>
public partial class PaymentStep {
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public DueSelectorEntryState State { get; set; } = default!;

    // Monthly payment may have become unavailable since it was picked, e.g. after switching to the underage due.
    protected override void OnParametersSet() {
        if (!State.IsScheduleAllowed(State.PaymentSchedule))
            State.PaymentSchedule = PaymentSchedule.Annual;
    }

    private string FullName() {
        return $"{State.FirstName} {State.LastName}".Trim();
    }

    private void UseNameAsAccountHolder() {
        State.AccountHolder = FullName();
    }

    // An IBAN still being typed is not flagged; the check starts once it has its minimum length.
    private bool IbanInvalid() {
        var iban = Iban.Normalize(State.IBAN);
        return iban.Length >= Iban.MinLength && !Iban.IsValid(iban);
    }

    private bool IsScheduleDisabled(PaymentSchedule schedule) {
        return !State.IsScheduleAllowed(schedule);
    }

    private static bool IsScheduleExcluded(PaymentSchedule schedule) {
        return schedule == PaymentSchedule.None;
    }

    private string TextForPaymentSchedule(PaymentSchedule schedule) {
        return schedule switch {
            PaymentSchedule.None => "",
            PaymentSchedule.Annual => I18n[I18nKey.Ui.PaymentOptionSelection.ScheduleAnnual],
            PaymentSchedule.Quarterly => I18n[I18nKey.Ui.PaymentOptionSelection.ScheduleQuarterly],
            PaymentSchedule.Monthly => I18n[I18nKey.Ui.PaymentOptionSelection.ScheduleMonthly],
            _ => throw new UnreachableException()
        };
    }
}
