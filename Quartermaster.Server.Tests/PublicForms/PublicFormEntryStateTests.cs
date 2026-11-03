using System;
using System.Threading.Tasks;
using Quartermaster.Api.DueSelector;
using Quartermaster.Blazor.Pages.DueSelector;
using Quartermaster.Blazor.Pages.MembershipApplication;

namespace Quartermaster.Server.Tests.PublicForms;

public class DueSelectorEntryStateTests {
    [Test]
    [Arguments(0, 72)]
    [Arguments(1999, 72)]
    [Arguments(2000, 120)]
    [Arguments(2500, 180)]
    [Arguments(3000, 240)]
    [Arguments(4000, 360)]
    [Arguments(5000, 480)]
    [Arguments(6000, 600)]
    public async Task Monthly_income_group_sets_the_due(int income, int due) {
        var state = new DueSelectorEntryState { SelectedValuation = SelectedValuation.MonthlyPayGroup, MonthlyIncomeGroup = income };
        state.RecalculateDue();
        await Assert.That(state.SelectedDue).IsEqualTo(due);
    }

    [Test]
    public async Task One_percent_takes_a_hundredth_of_the_yearly_income() {
        var state = new DueSelectorEntryState { SelectedValuation = SelectedValuation.OnePercentYearlyPay, YearlyIncome = 45_000 };
        state.RecalculateDue();
        await Assert.That(state.SelectedDue).IsEqualTo(450m);
    }

    [Test]
    public async Task One_percent_needs_the_minimum_income() {
        var state = new DueSelectorEntryState { SelectedValuation = SelectedValuation.OnePercentYearlyPay, YearlyIncome = 7_199 };
        await Assert.That(state.IsDuesComplete()).IsFalse();
    }

    [Test]
    public async Task Underage_pays_the_fixed_due() {
        var state = new DueSelectorEntryState { SelectedValuation = SelectedValuation.Underage };
        state.RecalculateDue();
        await Assert.That(state.SelectedDue).IsEqualTo(DueSelectorEntryState.UnderageDue);
    }

    [Test]
    public async Task New_form_starts_with_classification_slider_and_no_choices() {
        var state = new DueSelectorEntryState();

        await Assert.That(state.ReducedIncomeFallback).IsTrue();
        await Assert.That(state.MonthlyIncomeGroup).IsEqualTo(1500m);
        await Assert.That(state.ReducedAmount).IsNull();
        await Assert.That(state.IsDirectDeposit).IsNull();
    }

    [Test]
    public async Task Reduced_without_income_classification_records_the_default_due() {
        var state = new DueSelectorEntryState { SelectedValuation = SelectedValuation.Reduced, MonthlyIncomeGroup = 3000, ReducedIncomeFallback = false };
        state.RecalculateDue();

        await Assert.That(state.SelectedDue).IsEqualTo(DueSelectionRates.RejectedReductionDue);
        await Assert.That(state.ToDTO().MonthlyIncomeGroup).IsEqualTo(0m);
    }

    [Test]
    public async Task Classifying_again_after_a_skip_keeps_the_slider_value() {
        var state = new DueSelectorEntryState { SelectedValuation = SelectedValuation.Reduced, MonthlyIncomeGroup = 3000, ReducedIncomeFallback = false };
        state.RecalculateDue();

        state.ReducedIncomeFallback = true;
        state.RecalculateDue();

        await Assert.That(state.SelectedDue).IsEqualTo(240m);
        await Assert.That(state.ToDTO().MonthlyIncomeGroup).IsEqualTo(3000m);
    }

    [Test]
    public async Task Income_group_is_only_sent_when_it_classifies() {
        var state = new DueSelectorEntryState { SelectedValuation = SelectedValuation.OnePercentYearlyPay, YearlyIncome = 45_000 };
        await Assert.That(state.ToDTO().MonthlyIncomeGroup).IsEqualTo(0m);
    }

    [Test]
    public async Task Reduced_with_income_classification_uses_it_as_fallback() {
        var state = new DueSelectorEntryState { SelectedValuation = SelectedValuation.Reduced, MonthlyIncomeGroup = 3000, ReducedIncomeFallback = true };
        state.RecalculateDue();
        await Assert.That(state.SelectedDue).IsEqualTo(240m);
    }

    [Test]
    public async Task Reduced_needs_a_justification_and_an_amount() {
        var state = new DueSelectorEntryState { SelectedValuation = SelectedValuation.Reduced, ReducedAmount = 12, ReducedJustification = "" };
        await Assert.That(state.IsDuesComplete()).IsFalse();

        state.ReducedJustification = "Studium";
        await Assert.That(state.IsDuesComplete()).IsTrue();

        state.ReducedAmount = null;
        await Assert.That(state.IsDuesComplete()).IsFalse();
    }

    [Test]
    [Arguments(11, false)]
    [Arguments(12, true)]
    [Arguments(72, true)]
    [Arguments(73, false)]
    public async Task Reduced_amount_must_lie_within_the_range(int amount, bool valid) {
        var state = new DueSelectorEntryState { SelectedValuation = SelectedValuation.Reduced, ReducedAmount = amount, ReducedJustification = "Studium" };
        await Assert.That(state.IsDuesComplete()).IsEqualTo(valid);
    }

    [Test]
    public async Task Reduced_amount_maximum_follows_the_server() {
        var state = new DueSelectorEntryState { SelectedValuation = SelectedValuation.Reduced, ReducedAmount = 80, ReducedJustification = "Studium", ReducedMaximum = 84 };
        await Assert.That(state.IsDuesComplete()).IsTrue();
    }

    [Test]
    public async Task No_valuation_is_incomplete() {
        await Assert.That(new DueSelectorEntryState().IsDuesComplete()).IsFalse();
    }

    [Test]
    public async Task Direct_debit_needs_holder_and_valid_iban() {
        var state = new DueSelectorEntryState { IsDirectDeposit = true, AccountHolder = "Max Mustermann", IBAN = "DE89370400440532013001" };
        await Assert.That(state.IsPaymentComplete()).IsFalse();

        state.IBAN = "DE89370400440532013000";
        await Assert.That(state.IsPaymentComplete()).IsTrue();

        state.AccountHolder = " ";
        await Assert.That(state.IsPaymentComplete()).IsFalse();
    }

    [Test]
    public async Task Manual_transfer_needs_no_bank_details() {
        var state = new DueSelectorEntryState { IsDirectDeposit = false };
        await Assert.That(state.IsPaymentComplete()).IsTrue();
    }

    [Test]
    public async Task Payment_is_incomplete_until_a_method_is_chosen() {
        var state = new DueSelectorEntryState();
        await Assert.That(state.IsPaymentComplete()).IsFalse();
        await Assert.That(state.ToDTO().IsDirectDeposit).IsFalse();
    }

    [Test]
    public async Task Monthly_payment_is_not_offered_for_small_dues() {
        var underage = new DueSelectorEntryState { SelectedValuation = SelectedValuation.Underage };
        await Assert.That(underage.IsScheduleAllowed(PaymentSchedule.Monthly)).IsFalse();

        var reduced = new DueSelectorEntryState { SelectedValuation = SelectedValuation.Reduced, ReducedAmount = 35 };
        await Assert.That(reduced.IsScheduleAllowed(PaymentSchedule.Monthly)).IsFalse();

        reduced.ReducedAmount = 36;
        await Assert.That(reduced.IsScheduleAllowed(PaymentSchedule.Monthly)).IsTrue();
        await Assert.That(reduced.IsScheduleAllowed(PaymentSchedule.Quarterly)).IsTrue();
    }

    [Test]
    public async Task Iban_is_sent_normalized_for_direct_debit() {
        var state = new DueSelectorEntryState { IsDirectDeposit = true, IBAN = "de89 3704 0044 0532 0130 00" };
        await Assert.That(state.ToDTO().IBAN).IsEqualTo("DE89370400440532013000");
    }
}

public class MembershipApplicationEntryStateTests {
    /// <summary>Returning to the code step must not replace a code the applicant may already have in their mailbox.</summary>
    [Test]
    public async Task A_code_sent_to_the_address_counts_until_it_expires() {
        var now = new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);
        var state = new MembershipApplicationEntryState { Email = " Anna@Example.org " };
        state.RecordCodeSent("anna@example.org", now, lifetimeMinutes: 15);

        await Assert.That(state.HasUnexpiredCode(now.AddMinutes(14))).IsTrue();
        await Assert.That(state.HasUnexpiredCode(now.AddMinutes(15))).IsFalse();
    }

    [Test]
    public async Task A_code_sent_to_another_address_does_not_count() {
        var now = new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);
        var state = new MembershipApplicationEntryState { Email = "anna@example.org" };
        state.RecordCodeSent("anna@example.org", now, lifetimeMinutes: 15);
        state.Email = "bernd@example.org";

        await Assert.That(state.HasUnexpiredCode(now.AddMinutes(1))).IsFalse();
    }

    private static MembershipApplicationEntryState GermanAddress() {
        var state = new MembershipApplicationEntryState();
        state.SelectCountry(true, "Deutschland");
        state.AddressPostCode = "10115";
        state.AddressCity = "Berlin";
        state.AddressStreet = "Musterstraße";
        state.AddressHouseNbr = "1";
        state.AddressAdministrativeDivisionId = Guid.NewGuid();
        state.ChapterId = Guid.NewGuid();
        state.ChapterName = "Berlin";
        return state;
    }

    [Test]
    public async Task Address_is_incomplete_until_a_country_is_chosen() {
        var state = new MembershipApplicationEntryState {
            AddressPostCode = "10115",
            AddressCity = "Berlin",
            AddressStreet = "Musterstraße",
            AddressHouseNbr = "1"
        };
        await Assert.That(state.IsAddressComplete()).IsFalse();
    }

    [Test]
    public async Task Complete_german_address() {
        await Assert.That(GermanAddress().IsAddressComplete()).IsTrue();
    }

    [Test]
    public async Task Abroad_needs_the_country() {
        var state = GermanAddress();
        state.SelectCountry(false, "Deutschland");
        state.AddressPostCode = "1010";
        state.AddressCity = "Wien";
        state.AddressStreet = "Ring";
        state.AddressHouseNbr = "1";
        await Assert.That(state.IsAddressComplete()).IsFalse();

        state.AddressCountry = "Österreich";
        await Assert.That(state.IsAddressComplete()).IsTrue();
    }

    [Test]
    public async Task Changing_the_country_clears_address_and_chapter() {
        var state = GermanAddress();
        state.SelectCountry(false, "Deutschland");

        await Assert.That(state.AddressCity).IsEqualTo("");
        await Assert.That(state.AddressStreet).IsEqualTo("");
        await Assert.That(state.AddressAdministrativeDivisionId).IsNull();
        await Assert.That(state.ChapterId).IsNull();
        await Assert.That(state.AddressCountry).IsEqualTo("");
    }

    [Test]
    public async Task Other_party_membership_needs_the_parties_named() {
        var state = new MembershipApplicationEntryState { ConformityDeclarationAccepted = true };
        await Assert.That(state.AreDeclarationsComplete()).IsTrue();

        state.IsMemberOfAnotherParty = true;
        await Assert.That(state.AreDeclarationsComplete()).IsFalse();

        state.OtherParties = "Volt";
        await Assert.That(state.AreDeclarationsComplete()).IsTrue();
    }

    [Test]
    public async Task Personal_data_needs_all_required_fields() {
        var state = new MembershipApplicationEntryState {
            FirstName = "Max",
            LastName = "Mustermann",
            DateOfBirth = new DateTime(1990, 1, 1),
            Email = "max@example.com"
        };
        await Assert.That(state.IsPersonalDataComplete()).IsTrue();

        state.DateOfBirth = null;
        await Assert.That(state.IsPersonalDataComplete()).IsFalse();
    }
}
