using System;
using System.Collections.Generic;
using System.Linq;
using Markdig;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Quartermaster.Api.I18n;

namespace Quartermaster.Rendering;

public enum MotionMarkdownProblem {
    RawHtml = 0,
    Image = 1,
    TooDeeplyNested = 2,
    TooLong = 3,
    TableTooLarge = 4,
    TooManyBrackets = 5,
    TooMuchInlineMarkup = 6,
    OutputTooLarge = 7,
    TooManyLines = 8
}

public static class MotionMarkdown {
    public static MarkdownPipeline Pipeline { get; } = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)
        .Build();

    public const int MaxLength = 100_000;

    // Second line of defence behind MotionMarkdownLimits. Ordinary text grows by a factor below two; only stacks of nested lists and quotes get
    // past five times the length limit, and sanitizing their elements costs about a second per megabyte. Anything above is not stored or served.
    public const int MaxHtmlLength = 500_000;

    /// <summary>
    /// Returns the form in which motion Markdown is validated, rendered and stored: Unicode NFC, line feeds only, no trailing whitespace. Null yields an empty string.
    /// </summary>
    public static string Normalize(string? markdown) {
        return TextNormalization.ToNfc(markdown)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .TrimEnd();
    }

    /// <summary>
    /// Lists everything that keeps the text from being accepted as motion Markdown. The text is normalized first, so the result is the same for a text and its normalized form.
    /// </summary>
    public static List<MotionMarkdownProblem> FindProblems(string? markdown) {
        return Analyze(Normalize(markdown), MaxHtmlLength, out _);
    }

    /// <summary>
    /// Renders the normalized text with the motion pipeline; the result is not sanitized. Returns false (and an empty string) for every problem except raw HTML and images, which are left to the sanitizer.
    /// </summary>
    public static bool TryRender(string? markdown, out string html) {
        return TryRender(markdown, MaxHtmlLength, out html);
    }

    internal static bool TryRender(string? markdown, int maxHtmlLength, out string html) {
        var problems = Analyze(Normalize(markdown), maxHtmlLength, out html);
        return !problems.Any(BlocksRendering);
    }

    internal static List<MotionMarkdownProblem> FindProblems(string? markdown, int maxHtmlLength) {
        return Analyze(Normalize(markdown), maxHtmlLength, out _);
    }

    public static string MessageKey(MotionMarkdownProblem problem) {
        return problem switch {
            MotionMarkdownProblem.RawHtml => I18nKey.Error.AssemblyMotion.MarkdownHtmlNotAllowed,
            MotionMarkdownProblem.Image => I18nKey.Error.AssemblyMotion.MarkdownImageNotAllowed,
            MotionMarkdownProblem.TooDeeplyNested => I18nKey.Error.AssemblyMotion.MarkdownTooDeeplyNested,
            MotionMarkdownProblem.TooLong => I18nKey.Error.AssemblyMotion.MarkdownTooLong,
            MotionMarkdownProblem.TableTooLarge => I18nKey.Error.AssemblyMotion.MarkdownTableTooLarge,
            MotionMarkdownProblem.TooManyBrackets => I18nKey.Error.AssemblyMotion.MarkdownTooManyBrackets,
            MotionMarkdownProblem.TooMuchInlineMarkup => I18nKey.Error.AssemblyMotion.MarkdownTooMuchInlineMarkup,
            MotionMarkdownProblem.OutputTooLarge => I18nKey.Error.AssemblyMotion.MarkdownOutputTooLarge,
            MotionMarkdownProblem.TooManyLines => I18nKey.Error.AssemblyMotion.MarkdownTooManyLines,
            _ => throw new ArgumentOutOfRangeException(nameof(problem), problem, null)
        };
    }

    private static bool BlocksRendering(MotionMarkdownProblem problem) {
        return problem != MotionMarkdownProblem.RawHtml && problem != MotionMarkdownProblem.Image;
    }

    private static List<MotionMarkdownProblem> Analyze(string normalized, int maxHtmlLength, out string html) {
        html = "";
        if (normalized.Length == 0)
            return [];
        if (normalized.Length > MaxLength)
            return [MotionMarkdownProblem.TooLong];

        var problems = new HashSet<MotionMarkdownProblem>();
        MotionMarkdownLimits.Find(normalized, problems);
        if (problems.Count > 0)
            return Ordered(problems);

        MarkdownDocument document;
        string rendered;
        try {
            document = Markdown.Parse(normalized, Pipeline);
            DropTableAlignment(document);
            rendered = document.ToHtml(Pipeline);
        } catch (ArgumentException ex) when (ex.GetType() == typeof(ArgumentException)) {
            // Markdig signals its nesting limit only by exception. This class is static and shared with the WASM client,
            // so there is no logger; the exception is translated into a validation result.
            return [MotionMarkdownProblem.TooDeeplyNested];
        }

        if (rendered.Length > maxHtmlLength)
            return [MotionMarkdownProblem.OutputTooLarge];

        foreach (var node in document.Descendants()) {
            if (node is HtmlBlock || node is HtmlInline)
                problems.Add(MotionMarkdownProblem.RawHtml);

            if (node is LinkInline link && link.IsImage)
                problems.Add(MotionMarkdownProblem.Image);
        }

        html = rendered;
        return Ordered(problems);
    }

    // Markdig renders column alignment as a style attribute on every cell. The sanitizer removes it, but only after its CSS parser
    // has spent about 30 microseconds on each one, which is most of the cost of a large table.
    private static void DropTableAlignment(MarkdownDocument document) {
        foreach (var table in document.Descendants<Table>()) {
            foreach (var column in table.ColumnDefinitions)
                column.Alignment = null;
        }
    }

    private static List<MotionMarkdownProblem> Ordered(HashSet<MotionMarkdownProblem> problems) {
        return problems
            .OrderBy(problem => problem)
            .ToList();
    }
}
