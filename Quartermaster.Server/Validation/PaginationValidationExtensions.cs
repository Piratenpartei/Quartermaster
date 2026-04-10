using FluentValidation;
using Quartermaster.Api;
using Quartermaster.Api.I18n;

namespace Quartermaster.Server.Validation;

public static class PaginationValidationExtensions {
    public static void AddPaginationRules<T>(this AbstractValidator<T> validator)
        where T : IPaginatedRequest {

        validator.RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage(I18nKey.Error.Validation.PageMin);

        validator.RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage(I18nKey.Error.Validation.PageSizeRange);
    }
}
