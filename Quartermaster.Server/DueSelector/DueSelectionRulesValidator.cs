using FluentValidation;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.I18n;

namespace Quartermaster.Server.DueSelector;

/// <summary>
/// The rules of the dues form, which the browser applies too: a valuation, a due no lower than the stated income asks for, a
/// justification for a reduction, an allowed payment schedule and complete direct debit details. A plain FluentValidation
/// validator, so FastEndpoints doesn't register it for <see cref="DueSelectionDTO"/> next to <see cref="DueSelectionDTOValidator"/>;
/// both that one and the membership application validator include it. The limits that depend on settings are in <see cref="ReducedDueRules"/>.
/// </summary>
public class DueSelectionRulesValidator : AbstractValidator<DueSelectionDTO> {
    public DueSelectionRulesValidator() {
        RuleFor(x => x.SelectedValuation)
            .Must(v => v != SelectedValuation.None)
            .WithMessage(I18nKey.Error.Admin.DueSelection.ValuationRequired)
            .IsInEnum()
            .WithMessage(I18nKey.Error.Admin.DueSelection.ValuationRequired);

        RuleFor(x => x.YearlyIncome)
            .GreaterThanOrEqualTo(0)
            .WithMessage(I18nKey.Error.Admin.DueSelection.AmountNotNegative);

        RuleFor(x => x.MonthlyIncomeGroup)
            .GreaterThanOrEqualTo(0)
            .WithMessage(I18nKey.Error.Admin.DueSelection.AmountNotNegative);

        RuleFor(x => x.SelectedDue)
            .GreaterThanOrEqualTo(0)
            .WithMessage(I18nKey.Error.Admin.DueSelection.AmountNotNegative);

        // Without this check a low amount under a non-reduced valuation would skip the reduction decision.
        RuleFor(x => x.SelectedDue)
            .Must((dto, due) => due >= DueSelectionRates.MinimumDue(dto))
            .WithMessage(I18nKey.Error.Admin.DueSelection.DueBelowRate);

        RuleFor(x => x.ReducedJustification)
            .NotEmpty()
            .WithMessage(I18nKey.Error.Admin.DueSelection.JustificationRequired)
            .When(x => x.SelectedValuation == SelectedValuation.Reduced);

        RuleFor(x => x.ReducedJustification)
            .MaximumLength(2048)
            .WithMessage(I18nKey.Error.Admin.DueSelection.JustificationMaxLength);

        RuleFor(x => x.ReducedTimeSpan)
            .IsInEnum()
            .WithMessage(I18nKey.Error.Admin.DueSelection.TimeSpanInvalid);

        RuleFor(x => x.PaymentSchedule)
            .Must((dto, schedule) => DueSelectionRates.IsScheduleAllowed(dto.SelectedValuation, dto.ReducedAmount, schedule))
            .WithMessage(I18nKey.Error.Admin.DueSelection.ScheduleNotAllowed)
            .IsInEnum()
            .WithMessage(I18nKey.Error.Admin.DueSelection.ScheduleNotAllowed);

        RuleFor(x => x.AccountHolder)
            .MaximumLength(256)
            .WithMessage(I18nKey.Error.Admin.DueSelection.AccountHolderMaxLength);

        RuleFor(x => x.IBAN)
            .MaximumLength(64)
            .WithMessage(I18nKey.Error.Admin.DueSelection.IbanMaxLength);

        Include(new DirectDebitValidator());
    }
}
