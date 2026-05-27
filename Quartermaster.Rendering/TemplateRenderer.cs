using System.Collections.Generic;
using System.Threading.Tasks;
using Fluid;

namespace Quartermaster.Rendering;

public static class TemplateRenderer {
    private static readonly FluidParser Parser = new();

    /// <summary>
    /// Renders the Fluid template and returns the raw text output. Use this for channels
    /// that already speak markdown-like syntax (e.g. Telegram with <c>ParseMode.Markdown</c>)
    /// or for subject lines that must stay plain text.
    /// </summary>
    public static async Task<(string? Text, string? Error)> RenderTextAsync(
        string template, Dictionary<string, object> model) {

        if (!Parser.TryParse(template, out var parsed, out var error)) {
            return (null, $"Template parse error: {error}");
        }
        var context = new TemplateContext();
        foreach (var (key, value) in model) {
            context.SetValue(key, value);
        }
        var rendered = await parsed.RenderAsync(context);
        return (rendered, null);
    }

    /// <summary>
    /// Renders the Fluid template, then runs the result through Markdown → sanitized HTML.
    /// Use this for email bodies and template-preview UIs.
    /// </summary>
    public static async Task<(string? Html, string? Error)> RenderHtmlAsync(
        string template, Dictionary<string, object> model) {

        var (text, error) = await RenderTextAsync(template, model);
        if (error != null) {
            return (null, error);
        }
        return (MarkdownService.ToHtml(text!, SanitizationProfile.Standard), null);
    }
}
