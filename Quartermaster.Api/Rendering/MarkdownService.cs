using Markdig;

namespace Quartermaster.Api.Rendering;

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
}
