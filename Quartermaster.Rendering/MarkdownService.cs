using Markdig;

namespace Quartermaster.Rendering;

public static class MarkdownService {
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public static string ToHtml(string markdown, SanitizationProfile profile = SanitizationProfile.Standard) {
        if (string.IsNullOrEmpty(markdown))
            return "";

        var raw = Markdown.ToHtml(markdown, Pipeline);
        return HtmlSanitizationService.Sanitize(raw, profile);
    }

    /// <summary>
    /// Forces the one-time, expensive initialization of Markdig + the HtmlSanitizer's
    /// underlying AngleSharp parser. The first real <see cref="ToHtml"/> call otherwise
    /// pays ~1s; calling this at startup moves that cost off the request path.
    /// </summary>
    public static void Warmup() {
        ToHtml("# warmup\n\n**bold** [link](https://example.com)", SanitizationProfile.Strict);
        ToHtml("# warmup\n\n| a | b |\n| - | - |\n| 1 | 2 |", SanitizationProfile.Standard);
    }
}
