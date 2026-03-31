using Quartermaster.Api.Rendering;

namespace Quartermaster.Server.Options;

public static class TemplateRenderer {
    public static async System.Threading.Tasks.Task<(string? Html, string? Error)> RenderAsync(
        string markdownTemplate, System.Collections.Generic.Dictionary<string, object> model)
        => await Api.Rendering.TemplateRenderer.RenderAsync(markdownTemplate, model);
}
