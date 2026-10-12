using FluentValidation;
using Quartermaster.Api.I18n;
using Quartermaster.Rendering;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Members;

public static class AssemblyMotionMarkdownRules {
    public static IRuleBuilderOptionsConditions<T, string> RestrictedMarkdown<T>(this IRuleBuilder<T, string> rule)
        => rule.Custom((markdown, context) => {
            var problems = MotionMarkdown.FindProblems(markdown);
            if (problems.Contains(MotionMarkdownProblem.RawHtml))
                context.AddFailure(I18nKey.Error.AssemblyMotion.MarkdownHtmlNotAllowed);
            if (problems.Contains(MotionMarkdownProblem.Image))
                context.AddFailure(I18nKey.Error.AssemblyMotion.MarkdownImageNotAllowed);
            if (problems.Contains(MotionMarkdownProblem.TooDeeplyNested))
                context.AddFailure(I18nKey.Error.AssemblyMotion.MarkdownTooDeeplyNested);
            if (problems.Contains(MotionMarkdownProblem.TooLong))
                context.AddFailure(I18nKey.Error.AssemblyMotion.MarkdownTooLong);
        });
}
