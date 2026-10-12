using System;
using System.Collections.Generic;
using System.Linq;
using Markdig;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Quartermaster.Rendering;

public enum MotionMarkdownProblem {
    RawHtml = 0,
    Image = 1,
    TooDeeplyNested = 2,
    TooLong = 3
}

public static class MotionMarkdown {
    public static MarkdownPipeline Pipeline { get; } = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)
        .Build();

    public const int MaxLength = 100_000;

    public static List<MotionMarkdownProblem> FindProblems(string? markdown) {
        if (string.IsNullOrEmpty(markdown))
            return [];
        if (markdown.Length > MaxLength)
            return [MotionMarkdownProblem.TooLong];

        MarkdownDocument document;
        try {
            document = Markdown.Parse(markdown, Pipeline);
            document.ToHtml(Pipeline);
        } catch (ArgumentException ex) when (ex.GetType() == typeof(ArgumentException)) {
            // Markdig signals its nesting limit only by exception. This class is static and shared with the WASM client,
            // so there is no logger; the exception is translated into a validation result.
            return [MotionMarkdownProblem.TooDeeplyNested];
        }

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

    /// <summary>
    /// Renders with the motion pipeline. Returns false (and an empty string) for texts that are too long or that Markdig refuses as too deeply nested.
    /// </summary>
    public static bool TryRender(string markdown, out string html) {
        html = "";
        if (markdown.Length > MaxLength)
            return false;

        try {
            html = Markdown.ToHtml(markdown, Pipeline);
            return true;
        } catch (ArgumentException ex) when (ex.GetType() == typeof(ArgumentException)) {
            // see FindProblems
            return false;
        }
    }
}
