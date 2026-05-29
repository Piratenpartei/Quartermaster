using System;
using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.I18n;

namespace Quartermaster.Server.Admin;

public class MembershipApplicationLinkDivisionRequestValidator : Validator<MembershipApplicationLinkDivisionRequest> {
    public MembershipApplicationLinkDivisionRequestValidator() {
        RuleFor(x => x.Id)
            .NotEqual(Guid.Empty)
            .WithMessage(I18nKey.Error.Admin.Application.IdRequired);
    }
}
