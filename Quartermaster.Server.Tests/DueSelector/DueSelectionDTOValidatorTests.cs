using System;
using FluentValidation.TestHelper;
using Quartermaster.Api.DueSelector;
using Quartermaster.Server.DueSelector;

namespace Quartermaster.Server.Tests.DueSelector;

public class DueSelectionDTOValidatorTests {
    private readonly DueSelectionDTOValidator _validator = new();

    private static DueSelectionDTO ValidDueSelection() => new() {
        FirstName = "Max",
        LastName = "Mustermann",
        EMail = "max@example.com",
        MemberNumber = 12345,
        SelectedValuation = SelectedValuation.MonthlyPayGroup,
        YearlyIncome = 50000m,
        MonthlyIncomeGroup = 4000m,
        ReducedAmount = 12m,
        SelectedDue = 120m,
        ReducedJustification = "",
        ReducedTimeSpan = ReducedTimeSpan.OneYear,
        IsDirectDeposit = false,
        AccountHolder = "Max Mustermann",
        IBAN = "DE89370400440532013000",
        PaymentScedule = PaymentScedule.Annual
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
            .WithErrorMessage("Vorname darf nicht leer sein.");
    }

    [Test]
    public void EmptyLastName_ShouldHaveError() {
        var selection = ValidDueSelection();
        selection.LastName = "";

        var result = _validator.TestValidate(selection);

        result.ShouldHaveValidationErrorFor(x => x.LastName)
            .WithErrorMessage("Nachname darf nicht leer sein.");
    }

    [Test]
    public void EmptyEMail_ShouldHaveNoError() {
        var selection = ValidDueSelection();
        selection.EMail = "";

        var result = _validator.TestValidate(selection);

        result.ShouldNotHaveValidationErrorFor(x => x.EMail);
    }

    [Test]
    public void EMailWithoutAtSign_ShouldHaveError() {
        var selection = ValidDueSelection();
        selection.EMail = "invalid-email";

        var result = _validator.TestValidate(selection);

        result.ShouldHaveValidationErrorFor(x => x.EMail)
            .WithErrorMessage("E-Mail-Adresse muss ein @ enthalten.");
    }

    [Test]
    public void ValidEMail_ShouldHaveNoError() {
        var selection = ValidDueSelection();
        selection.EMail = "test@example.com";

        var result = _validator.TestValidate(selection);

        result.ShouldNotHaveValidationErrorFor(x => x.EMail);
    }

    [Test]
    public void NegativeSelectedDue_ShouldHaveError() {
        var selection = ValidDueSelection();
        selection.SelectedDue = -1m;

        var result = _validator.TestValidate(selection);

        result.ShouldHaveValidationErrorFor(x => x.SelectedDue)
            .WithErrorMessage("Beitrag darf nicht negativ sein.");
    }

    [Test]
    public void ZeroSelectedDue_ShouldHaveNoError() {
        var selection = ValidDueSelection();
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
            .WithErrorMessage("Kontoinhaber darf maximal 256 Zeichen lang sein.");
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
            .WithErrorMessage("IBAN darf maximal 64 Zeichen lang sein.");
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
            .WithErrorMessage("Begründung darf maximal 2048 Zeichen lang sein.");
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
}
