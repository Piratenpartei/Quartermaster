using System.Collections.Generic;
using System.Threading.Tasks;
using Fluid;
using Markdig;

namespace Quartermaster.Api.Rendering;

public static class TemplateRenderer {
    private static readonly FluidParser Parser = new();
    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public static async Task<(string? Html, string? Error)> RenderAsync(
        string markdownTemplate, Dictionary<string, object> model) {

        if (!Parser.TryParse(markdownTemplate, out var template, out var error))
            return (null, $"Template parse error: {error}");

        var context = new TemplateContext();
        foreach (var (key, value) in model)
            context.SetValue(key, value);

        var rendered = await template.RenderAsync(context);
        var html = Markdown.ToHtml(rendered, MarkdownPipeline);
        return (html, null);
    }
}
