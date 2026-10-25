using System.Threading.Tasks;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.I18n;
using Quartermaster.Server.DueSelector;

namespace Quartermaster.Server.Tests.DueSelector;

public class ReducedDueRulesTests {
    private static DueSelectionDTO Reduction(decimal amount, ReducedTimeSpan timeSpan = ReducedTimeSpan.OneYear) {
        return new DueSelectionDTO { SelectedValuation = SelectedValuation.Reduced, ReducedAmount = amount, ReducedTimeSpan = timeSpan };
    }

    [Test]
    [Arguments(12)]
    [Arguments(72)]
    public async Task Amount_within_the_range_is_fine(int amount) {
        await Assert.That(ReducedDueRules.Problem(Reduction(amount), 72, false)).IsNull();
    }

    [Test]
    [Arguments(11)]
    [Arguments(73)]
    public async Task Amount_outside_the_range_names_the_limits(int amount) {
        var problem = ReducedDueRules.Problem(Reduction(amount), 72, false);

        await Assert.That(problem).IsEqualTo(I18nParams.With(I18nKey.Error.Admin.DueSelection.ReducedAmountOutOfRange, ("min", "12"), ("max", "72")));
    }

    [Test]
    public async Task Permanent_reduction_needs_the_setting() {
        await Assert.That(ReducedDueRules.Problem(Reduction(24, ReducedTimeSpan.Permanent), 72, false))
            .IsEqualTo(I18nKey.Error.Admin.DueSelection.PermanentReductionDisabled);
        await Assert.That(ReducedDueRules.Problem(Reduction(24, ReducedTimeSpan.Permanent), 72, true)).IsNull();
    }

    [Test]
    public async Task Regular_selection_is_not_checked() {
        var dto = new DueSelectionDTO { SelectedValuation = SelectedValuation.MonthlyPayGroup, ReducedAmount = 500, ReducedTimeSpan = ReducedTimeSpan.Permanent };
        await Assert.That(ReducedDueRules.Problem(dto, 72, false)).IsNull();
    }

    [Test]
    public async Task Missing_classification_gets_the_default_due() {
        var dto = Reduction(24);

        ReducedDueRules.ApplyRejectionDefault(dto);

        await Assert.That(dto.SelectedDue).IsEqualTo(DueSelectionRates.RejectedReductionDue);
    }

    [Test]
    public async Task Classification_is_kept() {
        var dto = Reduction(24);
        dto.SelectedDue = 72;

        ReducedDueRules.ApplyRejectionDefault(dto);

        await Assert.That(dto.SelectedDue).IsEqualTo(72m);
    }
}
