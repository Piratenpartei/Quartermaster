using System;

namespace Quartermaster.Api.Templates;

public class TemplateListItemDTO {
    public Guid Id { get; set; }
    public string? Identifier { get; set; }
    public string DisplayName { get; set; } = "";
    public bool IsSystem { get; set; }
    public Guid? ChapterId { get; set; }
    public bool AllowsMemberFields { get; set; }
    public bool AllowsEventFields { get; set; }
    public bool AllowsChapterFields { get; set; }
}
