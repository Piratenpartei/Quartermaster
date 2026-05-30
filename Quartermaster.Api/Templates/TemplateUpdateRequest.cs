using System;

namespace Quartermaster.Api.Templates;

public class TemplateUpdateRequest {
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = "";
    public string? Subject { get; set; }
    public string Body { get; set; } = "";
    public bool AllowsMemberFields { get; set; }
    public bool AllowsEventFields { get; set; }
    public bool AllowsChapterFields { get; set; }
    public TemplateRenderMode RenderMode { get; set; }
}
