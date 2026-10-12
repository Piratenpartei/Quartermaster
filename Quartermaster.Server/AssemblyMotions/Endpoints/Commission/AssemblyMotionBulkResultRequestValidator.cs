using System;
using System.Linq;
using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Commission;

public class AssemblyMotionBulkResultRequestValidator : Validator<AssemblyMotionBulkResultRequest> {
    public const int MaxItems = 2000;

    public AssemblyMotionBulkResultRequestValidator() {
        RuleFor(x => x.Items)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .NotEmpty()
            .Must(items => items.Count <= MaxItems)
            .Must(items => items.Select(i => i?.MotionId).Distinct().Count() == items.Count)
            .WithMessage(I18nKey.Error.AssemblyMotion.ResultItemsInvalid);

        RuleForEach(x => x.Items)
            .Must(item => item != null && Enum.IsDefined(item.Result))
            .WithMessage(I18nKey.Error.AssemblyMotion.InvalidState);
    }
}
