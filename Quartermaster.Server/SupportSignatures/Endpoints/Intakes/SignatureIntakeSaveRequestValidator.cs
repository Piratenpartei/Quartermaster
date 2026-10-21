using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.I18n;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures.Endpoints.Intakes;

public class SignatureIntakeSaveRequestValidator : Validator<SignatureIntakeSaveRequest> {
    public SignatureIntakeSaveRequestValidator() {
        RuleFor(x => x.Count)
            .GreaterThan(0)
            .WithMessage(I18nKey.Error.SupportSignatures.Intake.CountInvalid);

        RuleFor(x => x.Note)
            .Must(note => note == null || note.Length <= SignatureIntake.NoteMaxLength)
            .WithMessage(I18nKey.Error.SupportSignatures.Intake.NoteTooLong);
    }
}
