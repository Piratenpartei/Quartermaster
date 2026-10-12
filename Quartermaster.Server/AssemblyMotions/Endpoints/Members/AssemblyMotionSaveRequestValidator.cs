using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Rendering;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Members;

public class AssemblyMotionSaveRequestValidator : Validator<AssemblyMotionSaveRequest> {
    public AssemblyMotionSaveRequestValidator() {
        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage(I18nKey.Error.AssemblyMotion.TitleRequired)
            .MaximumLength(500)
            .WithMessage(I18nKey.Error.AssemblyMotion.TitleMaxLength);

        RuleFor(x => x.Section)
            .MaximumLength(500)
            .WithMessage(I18nKey.Error.AssemblyMotion.SectionMaxLength);

        RuleFor(x => x.TextMarkdown)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(I18nKey.Error.AssemblyMotion.TextRequired)
            .Must(NoRawHtml)
            .WithMessage(I18nKey.Error.AssemblyMotion.MarkdownHtmlNotAllowed)
            .Must(NoImages)
            .WithMessage(I18nKey.Error.AssemblyMotion.MarkdownImageNotAllowed);

        RuleFor(x => x.ReasonMarkdown)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(I18nKey.Error.AssemblyMotion.ReasonRequired)
            .Must(NoRawHtml)
            .WithMessage(I18nKey.Error.AssemblyMotion.MarkdownHtmlNotAllowed)
            .Must(NoImages)
            .WithMessage(I18nKey.Error.AssemblyMotion.MarkdownImageNotAllowed);

        RuleFor(x => x.Summary)
            .MaximumLength(2000)
            .WithMessage(I18nKey.Error.AssemblyMotion.SummaryMaxLength);

        RuleFor(x => x.Keywords)
            .MaximumLength(500)
            .WithMessage(I18nKey.Error.AssemblyMotion.KeywordsMaxLength);

        RuleFor(x => x.PriorDiscussion)
            .MaximumLength(2000)
            .WithMessage(I18nKey.Error.AssemblyMotion.PriorDiscussionMaxLength);
    }

    private static bool NoRawHtml(string markdown) => !HasProblem(markdown, MotionMarkdownProblem.RawHtml);

    private static bool NoImages(string markdown) => !HasProblem(markdown, MotionMarkdownProblem.Image);

    private static bool HasProblem(string markdown, MotionMarkdownProblem problem)
        => MotionMarkdown.FindProblems(markdown).Contains(problem);
}
