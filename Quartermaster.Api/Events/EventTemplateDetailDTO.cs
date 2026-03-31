using System;

namespace Quartermaster.Api.Events;

public class EventTemplateDetailDTO {
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string PublicNameTemplate { get; set; } = "";
    public string? DescriptionTemplate { get; set; }
    public string Variables { get; set; } = "[]";
    public string ChecklistItemTemplates { get; set; } = "[]";
    public Guid? ChapterId { get; set; }
    public DateTime CreatedAt { get; set; }
}
