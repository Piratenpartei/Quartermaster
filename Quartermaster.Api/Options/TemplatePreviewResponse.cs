namespace Quartermaster.Api.Options;

public class TemplatePreviewResponse {
    public string RenderedHtml { get; set; } = "";
    public string? Error { get; set; }
}
