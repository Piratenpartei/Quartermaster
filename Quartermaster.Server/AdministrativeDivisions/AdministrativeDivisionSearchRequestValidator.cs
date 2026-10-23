using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.AdministrativeDivisions;
using Quartermaster.Api.I18n;
using Quartermaster.Server.Validation;

namespace Quartermaster.Server.AdministrativeDivisions;

public class AdministrativeDivisionSearchRequestValidator : Validator<AdministrativeDivisionSearchRequest> {
    public const int QueryMaxLength = 200;

    public AdministrativeDivisionSearchRequestValidator() {
        this.AddPaginationRules();

        RuleFor(x => x.Query)
            .MaximumLength(QueryMaxLength)
            .WithMessage(I18nKey.Error.Validation.QueryMaxLength);
    }
}
