using System.Collections.Generic;
using System.Threading.Tasks;
using Fluid;

namespace Quartermaster.Rendering;

public static class TemplateRenderer {
    private static readonly FluidParser Parser = CreateParser();

    private static FluidParser CreateParser() {
        var parser = new FluidParser();
        EnvelopeTags.Register(parser);
        return parser;
    }

    public static async Task<(string? Text, string? Error)> RenderTextAsync(
        string template, Dictionary<string, object> model) {
        var (text, error, _) = await RenderTextWithContextAsync(template, model);
        return (text, error);
    }

    public static async Task<(string? Text, string? Error, TemplateContext? Context)> RenderTextWithContextAsync(
        string template, Dictionary<string, object> model) {
        if (!Parser.TryParse(template, out var parsed, out var error)) {
            return (null, $"Template parse error: {error}", null);
        }
        var context = new TemplateContext();
        foreach (var (key, value) in model) {
            context.SetValue(key, value);
        }
        var rendered = await parsed.RenderAsync(context);
        return (rendered, null, context);
    }

    public static async Task<(string? Html, string? Error)> RenderHtmlAsync(
        string template, Dictionary<string, object> model) {
        var (text, error) = await RenderTextAsync(template, model);
        if (error != null) {
            return (null, error);
        }
        return (MarkdownService.ToHtml(text!, SanitizationProfile.Standard), null);
    }
}
