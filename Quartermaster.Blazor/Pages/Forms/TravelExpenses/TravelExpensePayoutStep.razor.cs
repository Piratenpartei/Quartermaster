using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Banking;
using Quartermaster.Api.I18n;
using Quartermaster.Api.TravelExpenses;

namespace Quartermaster.Blazor.Pages.Forms.TravelExpenses;

public partial class TravelExpensePayoutStep {
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public TravelExpenseInputDTO Input { get; set; } = default!;

    [Parameter, EditorRequired]
    public TravelExpenseCalculationDTO Calculation { get; set; } = default!;

    [Parameter]
    public bool CanUseReceipts { get; set; }

    [Parameter]
    public EventCallback OnChanged { get; set; }

    private Task Changed() {
        return OnChanged.InvokeAsync();
    }

    private Task SetMode(TravelDonationMode mode) {
        Input.DonationMode = mode;
        return Changed();
    }

    private Task SetPart(Action apply) {
        apply();
        return Changed();
    }

    // An IBAN still being typed is not flagged; the check starts once it has its minimum length.
    private bool IbanInvalid() {
        var iban = Iban.Normalize(Input.Iban);
        return iban.Length >= Iban.MinLength && !Iban.IsValid(iban);
    }
}
