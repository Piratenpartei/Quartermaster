using System.Collections.Generic;
using FluentValidation;
using Quartermaster.Api.I18n;
using Quartermaster.Api.TravelExpenses;

namespace Quartermaster.Server.TravelExpenses;

/// <summary>What a rules version must satisfy before the calculator can rely on it.</summary>
public class TravelExpenseRulesValidation : AbstractValidator<TravelExpenseRulesDTO> {
    public const int MaxRateSteps = 10;
    public const decimal MaxRatePerKm = 10;
    public const decimal MaxDailyRate = 1000;
    public const int MaxRulesTextLength = 100_000;

    public TravelExpenseRulesValidation() {
        RuleFor(x => x.ValidFrom).NotEmpty().WithMessage(I18nKey.Error.TravelExpense.RulesInvalid);
        RuleFor(x => x.VersionLabel).NotEmpty().WithMessage(I18nKey.Error.TravelExpense.RulesVersionLabelRequired);
        RuleFor(x => x.VersionLabel).MaximumLength(64).WithMessage(I18nKey.Error.TravelExpense.RulesInvalid);
        RuleFor(x => x.HeaderLines).MaximumLength(1024).WithMessage(I18nKey.Error.TravelExpense.RulesInvalid);
        RuleFor(x => x.RulesText).MaximumLength(MaxRulesTextLength).WithMessage(I18nKey.Error.TravelExpense.RulesInvalid);
        RuleFor(x => x.TemplateKey).NotEmpty().MaximumLength(128).WithMessage(I18nKey.Error.TravelExpense.RulesInvalid);
        RuleFor(x => x.CarRates).Must(steps => AreValidSteps(steps, allowEmpty: false)).WithMessage(I18nKey.Error.TravelExpense.RulesRateStepsInvalid);
        RuleFor(x => x.MotorcycleRates).Must(steps => AreValidSteps(steps, allowEmpty: true)).WithMessage(I18nKey.Error.TravelExpense.RulesRateStepsInvalid);
        RuleFor(x => x.MealDayTripRate).InclusiveBetween(0, MaxDailyRate).WithMessage(I18nKey.Error.TravelExpense.RulesInvalid);
        RuleFor(x => x.MealArrivalDepartureRate).InclusiveBetween(0, MaxDailyRate).WithMessage(I18nKey.Error.TravelExpense.RulesInvalid);
        RuleFor(x => x.MealFullDayRate).InclusiveBetween(0, MaxDailyRate).WithMessage(I18nKey.Error.TravelExpense.RulesInvalid);
        RuleFor(x => x.AccommodationFlatRate).InclusiveBetween(0, MaxDailyRate).WithMessage(I18nKey.Error.TravelExpense.RulesInvalid);
        RuleFor(x => x.BreakfastReductionPercent).InclusiveBetween(0, 100).WithMessage(I18nKey.Error.TravelExpense.RulesInvalid);
        RuleFor(x => x.LunchReductionPercent).InclusiveBetween(0, 100).WithMessage(I18nKey.Error.TravelExpense.RulesInvalid);
        RuleFor(x => x.DinnerReductionPercent).InclusiveBetween(0, 100).WithMessage(I18nKey.Error.TravelExpense.RulesInvalid);
        RuleFor(x => x.SubmissionDeadlineDays).InclusiveBetween(1, 3650).WithMessage(I18nKey.Error.TravelExpense.RulesInvalid);
        RuleFor(x => x.CutoffMonth).InclusiveBetween(1, 12).WithMessage(I18nKey.Error.TravelExpense.RulesInvalid);
        RuleFor(x => x.CutoffDay).InclusiveBetween(1, 31).WithMessage(I18nKey.Error.TravelExpense.RulesInvalid);
    }

    /// <summary>Limits ascend, only the last step is open-ended, and it must be: every kilometre needs a rate.</summary>
    public static bool AreValidSteps(List<RateStepDTO>? steps, bool allowEmpty) {
        if (steps == null || steps.Count == 0)
            return allowEmpty;
        if (steps.Count > MaxRateSteps)
            return false;
        if (steps[^1].UpToKm != null)
            return false;

        decimal previous = 0;
        for (var i = 0; i < steps.Count; i++) {
            if (steps[i].RatePerKm < 0 || steps[i].RatePerKm > MaxRatePerKm)
                return false;
            if (i == steps.Count - 1)
                break;

            var limit = steps[i].UpToKm;
            if (limit == null || limit <= previous)
                return false;
            previous = limit.Value;
        }
        return true;
    }
}
