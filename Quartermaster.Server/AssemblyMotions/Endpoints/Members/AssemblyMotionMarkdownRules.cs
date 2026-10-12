using FluentValidation;
using Quartermaster.Api.I18n;
using Quartermaster.Rendering;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Members;

public static class AssemblyMotionMarkdownRules {
    public static IRuleBuilderOptions<T, string> RestrictedMarkdown<T>(this IRuleBuilder<T, string> rule)
        => rule
            .Must(markdown => !MotionMarkdown.FindProblems(markdown).Contains(MotionMarkdownProblem.RawHtml))
            .WithMessage(I18nKey.Error.AssemblyMotion.MarkdownHtmlNotAllowed)
            .Must(markdown => !MotionMarkdown.FindProblems(markdown).Contains(MotionMarkdownProblem.Image))
            .WithMessage(I18nKey.Error.AssemblyMotion.MarkdownImageNotAllowed);
}
