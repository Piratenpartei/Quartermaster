using System.Collections.Generic;
using System.Linq;
using Markdig;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Quartermaster.Rendering;

public enum MotionMarkdownProblem {
    RawHtml = 0,
    Image = 1
}

public static class MotionMarkdown {
    public static MarkdownPipeline Pipeline { get; } = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)
        .Build();

    public static List<MotionMarkdownProblem> FindProblems(string? markdown) {
        if (string.IsNullOrEmpty(markdown))
            return [];

        var document = Markdown.Parse(markdown, Pipeline);
        var problems = new HashSet<MotionMarkdownProblem>();

        foreach (var node in document.Descendants()) {
            if (node is HtmlBlock || node is HtmlInline)
                problems.Add(MotionMarkdownProblem.RawHtml);

            if (node is LinkInline link && link.IsImage)
                problems.Add(MotionMarkdownProblem.Image);
        }

        return problems
            .OrderBy(problem => problem)
            .ToList();
    }
}
