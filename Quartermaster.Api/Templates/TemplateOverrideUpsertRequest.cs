using System;

namespace Quartermaster.Api.Templates;

public class TemplateOverrideUpsertRequest {
    public Guid TemplateId { get; set; }
    public Guid ChapterId { get; set; }
    public string? Subject { get; set; }
    public string Body { get; set; } = "";
}
