using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Commission;

public class AssemblyMotionCheckRequestValidator : Validator<AssemblyMotionCheckRequest> {
    public AssemblyMotionCheckRequestValidator() {
        RuleFor(x => x.Status)
            .IsInEnum()
            .WithMessage(I18nKey.Error.AssemblyMotion.InvalidState);

        RuleFor(x => x.CheckNote)
            .MaximumLength(AssemblyMotionContent.CheckNoteMaxLength)
            .WithMessage(I18nKey.Error.AssemblyMotion.CheckNoteMaxLength);
    }
}
