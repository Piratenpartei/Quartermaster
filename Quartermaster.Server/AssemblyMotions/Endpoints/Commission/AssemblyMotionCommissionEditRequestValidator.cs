using FluentValidation;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Server.AssemblyMotions.Endpoints.Members;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Commission;

public class AssemblyMotionCommissionEditRequestValidator : AssemblyMotionSaveFieldsValidator<AssemblyMotionCommissionEditRequest> {
    public AssemblyMotionCommissionEditRequestValidator() {
        RuleFor(x => x.ChangeNote)
            .MaximumLength(AssemblyMotionContent.ChangeNoteMaxLength)
            .WithMessage(I18nKey.Error.AssemblyMotion.ChangeNoteMaxLength);
    }
}
