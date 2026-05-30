using System;
using System.Collections.Generic;

namespace Quartermaster.Api.Events;

public class EventTemplateDetailDTO {
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string PublicNameTemplate { get; set; } = "";
    public string? DescriptionTemplate { get; set; }
    public List<EventTemplateVariableDTO> Variables { get; set; } = [];
    public List<EventChecklistItemTemplateDTO> ChecklistItemTemplates { get; set; } = [];
    public Guid? ChapterId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
