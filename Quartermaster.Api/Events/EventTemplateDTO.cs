using System;

namespace Quartermaster.Api.Events;

public class EventTemplateDTO {
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public int VariableCount { get; set; }
    public int ChecklistItemCount { get; set; }
    public Guid? ChapterId { get; set; }
    public DateTime CreatedAt { get; set; }
}
