using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.I18n;

namespace Quartermaster.Server.Admin;

public class MembershipApplicationProcessRequestValidator : Validator<MembershipApplicationProcessRequest> {
    public MembershipApplicationProcessRequestValidator() {
        RuleFor(x => x.Id)
            .NotEqual(System.Guid.Empty)
            .WithMessage(I18nKey.Error.Admin.Application.IdRequired);

        RuleFor(x => x.Status)
            .InclusiveBetween(1, 2)
            .WithMessage(I18nKey.Error.Admin.Application.StatusInvalid);
    }
}
