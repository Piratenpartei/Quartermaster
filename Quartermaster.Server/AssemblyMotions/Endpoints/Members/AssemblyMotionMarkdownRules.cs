using FluentValidation;
using Quartermaster.Rendering;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Members;

public static class AssemblyMotionMarkdownRules {
    public static IRuleBuilderOptionsConditions<T, string> RestrictedMarkdown<T>(this IRuleBuilder<T, string> rule) {
        return rule.Custom((markdown, context) => {
                foreach (var problem in MotionMarkdown.FindProblems(markdown))
                    context.AddFailure(MotionMarkdown.MessageKey(problem));
            });
    }
}
