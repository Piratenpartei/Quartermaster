using System;
using System.Collections.Generic;

namespace Quartermaster.Api.Templates;

public class TemplateDetailDTO {
    public Guid Id { get; set; }
    public string? Identifier { get; set; }
    public string DisplayName { get; set; } = "";
    public bool IsSystem { get; set; }
    public Guid? ChapterId { get; set; }
    public Guid? BaseTemplateId { get; set; }
    public string? ChapterName { get; set; }
    public string? Subject { get; set; }
    public string Body { get; set; } = "";
    public bool AllowsMemberFields { get; set; }
    public bool AllowsEventFields { get; set; }
    public bool AllowsChapterFields { get; set; }
    public TemplateRenderMode RenderMode { get; set; }
    public List<TemplateOverrideDTO> Overrides { get; set; } = new();
}
