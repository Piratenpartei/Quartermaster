using System;

namespace Quartermaster.Api.Templates;

public class TemplateOverrideDTO {
    public Guid Id { get; set; }
    public Guid ChapterId { get; set; }
    public string ChapterName { get; set; } = "";
    public string ChapterShortCode { get; set; } = "";
    public string? Subject { get; set; }
    public string Body { get; set; } = "";
}
