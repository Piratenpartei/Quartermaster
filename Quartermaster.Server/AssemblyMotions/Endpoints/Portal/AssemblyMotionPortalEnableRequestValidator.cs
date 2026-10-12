using System;
using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Portal;

public class AssemblyMotionPortalEnableRequestValidator : Validator<AssemblyMotionPortalEnableRequest> {
    public AssemblyMotionPortalEnableRequestValidator() {
        RuleFor(x => x.EventId)
            .NotEqual(Guid.Empty)
            .WithMessage(I18nKey.Error.AssemblyMotion.Portal.EventRequired);

        RuleFor(x => x.ClosesAt)
            .GreaterThan(x => x.OpensAt)
            .WithMessage(I18nKey.Error.AssemblyMotion.Portal.ClosesBeforeOpens);
    }
}
