using System;

namespace Quartermaster.Api.Templates;

public class TemplateCreateRequest {
    public string DisplayName { get; set; } = "";
    public Guid? ChapterId { get; set; }
    public string? Subject { get; set; }
    public string Body { get; set; } = "";
    public bool AllowsMemberFields { get; set; }
    public bool AllowsEventFields { get; set; }
    public bool AllowsChapterFields { get; set; }
}
