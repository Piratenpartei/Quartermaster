using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Motions;

namespace Quartermaster.Server.Motions;

public class MotionCreateRequestValidator : Validator<MotionCreateRequest> {
    public MotionCreateRequestValidator() {
        RuleFor(x => x.ChapterId)
            .NotEqual(System.Guid.Empty)
            .WithMessage(I18nKey.Error.Motion.ChapterRequired);

        RuleFor(x => x.AuthorName)
            .NotEmpty()
            .WithMessage(I18nKey.Error.Motion.SubmitterNameRequired)
            .MaximumLength(256)
            .WithMessage(I18nKey.Error.Motion.SubmitterNameMaxLength);

        RuleFor(x => x.AuthorEMail)
            .NotEmpty()
            .WithMessage(I18nKey.Error.Motion.EmailRequired)
            .Must(e => e != null && e.Contains('@'))
            .WithMessage(I18nKey.Error.Motion.EmailInvalid)
            .MaximumLength(256)
            .WithMessage(I18nKey.Error.Motion.EmailMaxLength);

        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage(I18nKey.Error.Motion.TitleRequired)
            .MaximumLength(512)
            .WithMessage(I18nKey.Error.Motion.TitleMaxLength);

        RuleFor(x => x.Text)
            .NotEmpty()
            .WithMessage(I18nKey.Error.Motion.BodyRequired)
            .MaximumLength(8192)
            .WithMessage(I18nKey.Error.Motion.BodyMaxLength);
    }
}
