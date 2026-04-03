using FluentValidation;
using Quartermaster.Api;

namespace Quartermaster.Server.Validation;

public static class PaginationValidationExtensions {
    public static void AddPaginationRules<T>(this AbstractValidator<T> validator)
        where T : IPaginatedRequest {

        validator.RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Seite muss mindestens 1 sein.");

        validator.RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Seitengröße muss zwischen 1 und 100 liegen.");
    }
}
