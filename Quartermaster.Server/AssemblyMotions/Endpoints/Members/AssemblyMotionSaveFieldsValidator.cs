using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Members;

public abstract class AssemblyMotionSaveFieldsValidator<T> : Validator<T> where T : AssemblyMotionSaveRequest {
    protected AssemblyMotionSaveFieldsValidator() {
        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage(I18nKey.Error.AssemblyMotion.TitleRequired)
            .MaximumLength(AssemblyMotionContent.TitleMaxLength)
            .WithMessage(I18nKey.Error.AssemblyMotion.TitleMaxLength);

        RuleFor(x => x.Section)
            .MaximumLength(AssemblyMotionContent.SectionMaxLength)
            .WithMessage(I18nKey.Error.AssemblyMotion.SectionMaxLength);

        RuleFor(x => x.TextMarkdown)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(I18nKey.Error.AssemblyMotion.TextRequired)
            .RestrictedMarkdown();

        RuleFor(x => x.ReasonMarkdown)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(I18nKey.Error.AssemblyMotion.ReasonRequired)
            .RestrictedMarkdown();

        RuleFor(x => x.Summary)
            .MaximumLength(AssemblyMotionContent.SummaryMaxLength)
            .WithMessage(I18nKey.Error.AssemblyMotion.SummaryMaxLength);

        RuleFor(x => x.Keywords)
            .MaximumLength(AssemblyMotionContent.KeywordsMaxLength)
            .WithMessage(I18nKey.Error.AssemblyMotion.KeywordsMaxLength);

        RuleFor(x => x.PriorDiscussion)
            .MaximumLength(AssemblyMotionContent.PriorDiscussionMaxLength)
            .WithMessage(I18nKey.Error.AssemblyMotion.PriorDiscussionMaxLength);
    }
}
