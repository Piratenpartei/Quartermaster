using System;
using FluentValidation.TestHelper;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.I18n;
using Quartermaster.Server.DueSelector;

namespace Quartermaster.Server.Tests.DueSelector;

public class DueSelectionDTOValidatorTests {
    private readonly DueSelectionDTOValidator _validator = new();

    private static DueSelectionDTO ValidDueSelection() => new() {
        FirstName = "Max",
        LastName = "Mustermann",
        Email = "max@example.com",
        MemberNumber = 12345,
        SelectedValuation = SelectedValuation.MonthlyPayGroup,
        YearlyIncome = 50000m,
        MonthlyIncomeGroup = 2000m,
        ReducedAmount = 12m,
        SelectedDue = 120m,
        ReducedJustification = "",
        ReducedTimeSpan = ReducedTimeSpan.OneYear,
        IsDirectDeposit = false,
        AccountHolder = "Max Mustermann",
        IBAN = "DE89370400440532013000",
        PaymentSchedule = PaymentSchedule.Annual
    };

    [Test]
    public void ValidDueSelection_ShouldHaveNoErrors() {
        var selection = ValidDueSelection();

        var result = _validator.TestValidate(selection);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void EmptyFirstName_ShouldHaveError() {
        var selection = ValidDueSelection();
        selection.FirstName = "";

        var result = _validator.TestValidate(selection);

        result.ShouldHaveValidationErrorFor(x => x.FirstName)
            .WithErrorMessage(I18nKey.Error.Admin.DueSelection.FirstNameRequired);
    }

    [Test]
    public void EmptyLastName_ShouldHaveError() {
        var selection = ValidDueSelection();
        selection.LastName = "";

        var result = _validator.TestValidate(selection);

        result.ShouldHaveValidationErrorFor(x => x.LastName)
            .WithErrorMessage(I18nKey.Error.Admin.DueSelection.LastNameRequired);
    }

    [Test]
    public void EmptyEmail_ShouldHaveNoError() {
        var selection = ValidDueSelection();
        selection.Email = "";

        var result = _validator.TestValidate(selection);

        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    [Test]
    public void EmailWithoutAtSign_ShouldHaveError() {
        var selection = ValidDueSelection();
        selection.Email = "invalid-email";

        var result = _validator.TestValidate(selection);

        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage(I18nKey.Error.Admin.DueSelection.EmailInvalid);
    }

    [Test]
    public void ValidEmail_ShouldHaveNoError() {
        var selection = ValidDueSelection();
        selection.Email = "test@example.com";

        var result = _validator.TestValidate(selection);

        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    [Test]
    public void NegativeSelectedDue_ShouldHaveError() {
        var selection = ValidDueSelection();
        selection.SelectedDue = -1m;

        var result = _validator.TestValidate(selection);

        result.ShouldHaveValidationErrorFor(x => x.SelectedDue)
            .WithErrorMessage(I18nKey.Error.Admin.DueSelection.AmountNotNegative);
    }

    [Test]
    public void ZeroSelectedDue_ShouldHaveNoError() {
        var selection = ValidDueSelection();
        selection.SelectedValuation = SelectedValuation.Reduced;
        selection.MonthlyIncomeGroup = 0;
        selection.SelectedDue = 0m;

        var result = _validator.TestValidate(selection);

        result.ShouldNotHaveValidationErrorFor(x => x.SelectedDue);
    }

    [Test]
    public void PositiveSelectedDue_ShouldHaveNoError() {
        var selection = ValidDueSelection();
        selection.SelectedDue = 120m;

        var result = _validator.TestValidate(selection);

        result.ShouldNotHaveValidationErrorFor(x => x.SelectedDue);
    }

    [Test]
    public void AccountHolderExceedsMaxLength_ShouldHaveError() {
        var selection = ValidDueSelection();
        selection.AccountHolder = new string('A', 257);

        var result = _validator.TestValidate(selection);

        result.ShouldHaveValidationErrorFor(x => x.AccountHolder)
            .WithErrorMessage(I18nKey.Error.Admin.DueSelection.AccountHolderMaxLength);
    }

    [Test]
    public void AccountHolderAtMaxLength_ShouldHaveNoError() {
        var selection = ValidDueSelection();
        selection.AccountHolder = new string('A', 256);

        var result = _validator.TestValidate(selection);

        result.ShouldNotHaveValidationErrorFor(x => x.AccountHolder);
    }

    [Test]
    public void EmptyAccountHolder_ShouldHaveNoError() {
        var selection = ValidDueSelection();
        selection.AccountHolder = "";

        var result = _validator.TestValidate(selection);

        result.ShouldNotHaveValidationErrorFor(x => x.AccountHolder);
    }

    [Test]
    public void IBANExceedsMaxLength_ShouldHaveError() {
        var selection = ValidDueSelection();
        selection.IBAN = new string('A', 65);

        var result = _validator.TestValidate(selection);

        result.ShouldHaveValidationErrorFor(x => x.IBAN)
            .WithErrorMessage(I18nKey.Error.Admin.DueSelection.IbanMaxLength);
    }

    [Test]
    public void IBANAtMaxLength_ShouldHaveNoError() {
        var selection = ValidDueSelection();
        selection.IBAN = new string('A', 64);

        var result = _validator.TestValidate(selection);

        result.ShouldNotHaveValidationErrorFor(x => x.IBAN);
    }

    [Test]
    public void EmptyIBAN_ShouldHaveNoError() {
        var selection = ValidDueSelection();
        selection.IBAN = "";

        var result = _validator.TestValidate(selection);

        result.ShouldNotHaveValidationErrorFor(x => x.IBAN);
    }

    [Test]
    public void ReducedJustificationExceedsMaxLength_ShouldHaveError() {
        var selection = ValidDueSelection();
        selection.ReducedJustification = new string('A', 2049);

        var result = _validator.TestValidate(selection);

        result.ShouldHaveValidationErrorFor(x => x.ReducedJustification)
            .WithErrorMessage(I18nKey.Error.Admin.DueSelection.JustificationMaxLength);
    }

    [Test]
    public void ReducedJustificationAtMaxLength_ShouldHaveNoError() {
        var selection = ValidDueSelection();
        selection.ReducedJustification = new string('A', 2048);

        var result = _validator.TestValidate(selection);

        result.ShouldNotHaveValidationErrorFor(x => x.ReducedJustification);
    }

    [Test]
    public void EmptyReducedJustification_ShouldHaveNoError() {
        var selection = ValidDueSelection();
        selection.ReducedJustification = "";

        var result = _validator.TestValidate(selection);

        result.ShouldNotHaveValidationErrorFor(x => x.ReducedJustification);
    }

    [Test]
    public void DirectDebitWithValidIban_ShouldHaveNoErrors() {
        var selection = ValidDueSelection();
        selection.IsDirectDeposit = true;
        selection.IBAN = "DE89 3704 0044 0532 0130 00";

        var result = _validator.TestValidate(selection);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void DirectDebitWithMistypedIban_ShouldHaveError() {
        var selection = ValidDueSelection();
        selection.IsDirectDeposit = true;
        selection.IBAN = "DE89370400440532013001";

        var result = _validator.TestValidate(selection);

        result.ShouldHaveValidationErrorFor(x => x.IBAN)
            .WithErrorMessage(I18nKey.Error.Admin.DueSelection.IbanInvalid);
    }

    [Test]
    public void DirectDebitWithoutAccountHolder_ShouldHaveError() {
        var selection = ValidDueSelection();
        selection.IsDirectDeposit = true;
        selection.AccountHolder = "";

        var result = _validator.TestValidate(selection);

        result.ShouldHaveValidationErrorFor(x => x.AccountHolder)
            .WithErrorMessage(I18nKey.Error.Admin.DueSelection.AccountHolderRequired);
    }

    [Test]
    public void ManualTransfer_DoesNotCheckTheIban() {
        var selection = ValidDueSelection();
        selection.IsDirectDeposit = false;
        selection.IBAN = "not an iban";
        selection.AccountHolder = "";

        var result = _validator.TestValidate(selection);

        result.ShouldNotHaveValidationErrorFor(x => x.IBAN);
        result.ShouldNotHaveValidationErrorFor(x => x.AccountHolder);
    }

    [Test]
    [Arguments(SelectedValuation.OnePercentYearlyPay, 5000, 50)]
    [Arguments(SelectedValuation.OnePercentYearlyPay, 10000, 50)]
    [Arguments(SelectedValuation.MonthlyPayGroup, 0, 50)]
    [Arguments(SelectedValuation.Underage, 0, 0)]
    public void Due_below_the_valuation_rate_ShouldHaveError(SelectedValuation valuation, int yearlyIncome, int due) {
        var selection = ValidDueSelection();
        selection.SelectedValuation = valuation;
        selection.YearlyIncome = yearlyIncome;
        selection.MonthlyIncomeGroup = 0;
        selection.SelectedDue = due;

        var result = _validator.TestValidate(selection);

        result.ShouldHaveValidationErrorFor(x => x.SelectedDue)
            .WithErrorMessage(I18nKey.Error.Admin.DueSelection.DueBelowRate);
    }

    [Test]
    [Arguments(SelectedValuation.OnePercentYearlyPay, 10000, 100)]
    [Arguments(SelectedValuation.MonthlyPayGroup, 0, 72)]
    [Arguments(SelectedValuation.Underage, 0, 12)]
    [Arguments(SelectedValuation.Reduced, 0, 0)]
    public void Due_at_the_valuation_rate_ShouldHaveNoError(SelectedValuation valuation, int yearlyIncome, int due) {
        var selection = ValidDueSelection();
        selection.SelectedValuation = valuation;
        selection.YearlyIncome = yearlyIncome;
        selection.MonthlyIncomeGroup = 0;
        selection.SelectedDue = due;

        var result = _validator.TestValidate(selection);

        result.ShouldNotHaveValidationErrorFor(x => x.SelectedDue);
    }

    [Test]
    [Arguments(SelectedValuation.MonthlyPayGroup, 120)]
    [Arguments(SelectedValuation.Reduced, 72)]
    public void Due_below_the_stated_income_group_ShouldHaveError(SelectedValuation valuation, int due) {
        var selection = ValidDueSelection();
        selection.SelectedValuation = valuation;
        selection.ReducedJustification = "Studium";
        selection.MonthlyIncomeGroup = 3000;
        selection.SelectedDue = due;

        var result = _validator.TestValidate(selection);

        result.ShouldHaveValidationErrorFor(x => x.SelectedDue)
            .WithErrorMessage(I18nKey.Error.Admin.DueSelection.DueBelowRate);
    }

    [Test]
    public void NoValuation_ShouldHaveError() {
        var selection = ValidDueSelection();
        selection.SelectedValuation = SelectedValuation.None;

        var result = _validator.TestValidate(selection);

        result.ShouldHaveValidationErrorFor(x => x.SelectedValuation)
            .WithErrorMessage(I18nKey.Error.Admin.DueSelection.ValuationRequired);
    }

    [Test]
    public void UndefinedValuation_ShouldHaveError() {
        var selection = ValidDueSelection();
        selection.SelectedValuation = (SelectedValuation)42;

        var result = _validator.TestValidate(selection);

        result.ShouldHaveValidationErrorFor(x => x.SelectedValuation);
    }

    [Test]
    public void ReductionWithoutJustification_ShouldHaveError() {
        var selection = ValidDueSelection();
        selection.SelectedValuation = SelectedValuation.Reduced;
        selection.ReducedJustification = " ";

        var result = _validator.TestValidate(selection);

        result.ShouldHaveValidationErrorFor(x => x.ReducedJustification)
            .WithErrorMessage(I18nKey.Error.Admin.DueSelection.JustificationRequired);
    }

    [Test]
    public void NegativeIncome_ShouldHaveError() {
        var selection = ValidDueSelection();
        selection.MonthlyIncomeGroup = -1;

        var result = _validator.TestValidate(selection);

        result.ShouldHaveValidationErrorFor(x => x.MonthlyIncomeGroup);
    }

    [Test]
    public void UndefinedTimeSpan_ShouldHaveError() {
        var selection = ValidDueSelection();
        selection.ReducedTimeSpan = (ReducedTimeSpan)7;

        var result = _validator.TestValidate(selection);

        result.ShouldHaveValidationErrorFor(x => x.ReducedTimeSpan)
            .WithErrorMessage(I18nKey.Error.Admin.DueSelection.TimeSpanInvalid);
    }

    [Test]
    [Arguments(SelectedValuation.Underage, 0, PaymentSchedule.Monthly)]
    [Arguments(SelectedValuation.Reduced, 35, PaymentSchedule.Monthly)]
    [Arguments(SelectedValuation.MonthlyPayGroup, 0, PaymentSchedule.None)]
    public void Schedule_not_offered_for_the_due_ShouldHaveError(SelectedValuation valuation, int reducedAmount, PaymentSchedule schedule) {
        var selection = ValidDueSelection();
        selection.SelectedValuation = valuation;
        selection.ReducedAmount = reducedAmount;
        selection.ReducedJustification = "Studium";
        selection.PaymentSchedule = schedule;

        var result = _validator.TestValidate(selection);

        result.ShouldHaveValidationErrorFor(x => x.PaymentSchedule)
            .WithErrorMessage(I18nKey.Error.Admin.DueSelection.ScheduleNotAllowed);
    }

    [Test]
    public void Monthly_schedule_for_a_regular_due_ShouldHaveNoError() {
        var selection = ValidDueSelection();
        selection.PaymentSchedule = PaymentSchedule.Monthly;

        var result = _validator.TestValidate(selection);

        result.ShouldNotHaveValidationErrorFor(x => x.PaymentSchedule);
    }
}
