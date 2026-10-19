using FluentValidation;
using Quartermaster.Api.I18n;
using Quartermaster.Api.TravelExpenses;

namespace Quartermaster.Server.TravelExpenses;

/// <summary>Size limits on everything the form sends; the content itself is checked by the calculator's notes, never refused.</summary>
public class TravelExpenseInputRules : AbstractValidator<TravelExpenseInputDTO> {
    public const int MaxModules = 200;
    public const int MaxReceiptsPerModule = 50;
    public const decimal MaxAmount = 1_000_000;
    public const decimal MaxKm = 100_000;

    public TravelExpenseInputRules() {
        RuleFor(x => x.FirstName).MaximumLength(128).WithMessage(I18nKey.Error.TravelExpense.InputInvalid);
        RuleFor(x => x.LastName).MaximumLength(128).WithMessage(I18nKey.Error.TravelExpense.InputInvalid);
        RuleFor(x => x.Email).MaximumLength(256).WithMessage(I18nKey.Error.TravelExpense.InputInvalid);
        RuleFor(x => x.Street).MaximumLength(256).WithMessage(I18nKey.Error.TravelExpense.InputInvalid);
        RuleFor(x => x.PostCode).MaximumLength(16).WithMessage(I18nKey.Error.TravelExpense.InputInvalid);
        RuleFor(x => x.City).MaximumLength(128).WithMessage(I18nKey.Error.TravelExpense.InputInvalid);
        RuleFor(x => x.MembershipChapterName).MaximumLength(256).WithMessage(I18nKey.Error.TravelExpense.InputInvalid);
        RuleFor(x => x.TravelChapterName).MaximumLength(256).WithMessage(I18nKey.Error.TravelExpense.InputInvalid);
        RuleFor(x => x.Function).MaximumLength(128).WithMessage(I18nKey.Error.TravelExpense.InputInvalid);
        RuleFor(x => x.Purpose).MaximumLength(1000).WithMessage(I18nKey.Error.TravelExpense.InputInvalid);
        RuleFor(x => x.Destination).MaximumLength(256).WithMessage(I18nKey.Error.TravelExpense.InputInvalid);
        RuleFor(x => x.ResolutionNumber).MaximumLength(64).WithMessage(I18nKey.Error.TravelExpense.InputInvalid);
        RuleFor(x => x.Start.Place).MaximumLength(256).WithMessage(I18nKey.Error.TravelExpense.InputInvalid);
        RuleFor(x => x.End.Place).MaximumLength(256).WithMessage(I18nKey.Error.TravelExpense.InputInvalid);
        RuleFor(x => x.Iban).MaximumLength(64).WithMessage(I18nKey.Error.TravelExpense.InputInvalid);
        RuleFor(x => x.Bic).MaximumLength(16).WithMessage(I18nKey.Error.TravelExpense.InputInvalid);
        RuleFor(x => x.DonationAmount).InclusiveBetween(0, MaxAmount).WithMessage(I18nKey.Error.TravelExpense.InputInvalid);
        RuleFor(x => x.PaperAttachmentCount).InclusiveBetween(0, 1000).WithMessage(I18nKey.Error.TravelExpense.InputInvalid);
        RuleFor(x => x.Modules.Count).LessThanOrEqualTo(MaxModules).WithMessage(I18nKey.Error.TravelExpense.InputInvalid);
        RuleForEach(x => x.Modules).SetValidator(new ModuleRules());
    }

    private class ModuleRules : AbstractValidator<TripModuleDTO> {
        public ModuleRules() {
            RuleFor(x => x.From).MaximumLength(256).WithMessage(I18nKey.Error.TravelExpense.InputInvalid);
            RuleFor(x => x.To).MaximumLength(256).WithMessage(I18nKey.Error.TravelExpense.InputInvalid);
            RuleFor(x => x.Description).MaximumLength(500).WithMessage(I18nKey.Error.TravelExpense.InputInvalid);
            RuleFor(x => x.Km).InclusiveBetween(0, MaxKm).WithMessage(I18nKey.Error.TravelExpense.InputInvalid);
            RuleFor(x => x.Amount).InclusiveBetween(0, MaxAmount).WithMessage(I18nKey.Error.TravelExpense.InputInvalid);
            RuleFor(x => x.BreakfastAmountPerNight).InclusiveBetween(0, MaxAmount).When(x => x.BreakfastAmountPerNight != null)
                .WithMessage(I18nKey.Error.TravelExpense.InputInvalid);
            RuleFor(x => x.ReceiptIds.Count).LessThanOrEqualTo(MaxReceiptsPerModule).WithMessage(I18nKey.Error.TravelExpense.InputInvalid);
        }
    }
}
