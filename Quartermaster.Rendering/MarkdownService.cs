using Markdig;

namespace Quartermaster.Rendering;

public static class MarkdownService {
    private static readonly MarkdownPipeline FullPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    /// <summary>
    /// Renders Markdown to sanitized HTML. The motion flavour always uses the motion sanitizer profile, whatever profile is passed,
    /// and yields an empty string for a text that <see cref="MotionMarkdown.TryRender(string?, out string)"/> refuses.
    /// </summary>
    public static string ToHtml(string markdown, SanitizationProfile profile = SanitizationProfile.Standard, MarkdownFlavor flavor = MarkdownFlavor.Full) {
        if (string.IsNullOrEmpty(markdown))
            return "";

        if (flavor == MarkdownFlavor.Motion) {
            return MotionMarkdown.TryRender(markdown, out var motionHtml)
                ? HtmlSanitizationService.Sanitize(motionHtml, SanitizationProfile.Motion)
                : "";
        }

        return HtmlSanitizationService.Sanitize(Markdown.ToHtml(markdown, FullPipeline), profile);
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
