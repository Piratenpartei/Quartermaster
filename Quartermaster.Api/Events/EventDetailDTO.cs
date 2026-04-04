using System;
using System.Collections.Generic;

namespace Quartermaster.Api.Events;

public class EventDetailDTO {
    public Guid Id { get; set; }
    public Guid ChapterId { get; set; }
    public string ChapterName { get; set; } = "";
    public string InternalName { get; set; } = "";
    public string PublicName { get; set; } = "";
    public string? Description { get; set; }
    public DateTime? EventDate { get; set; }
    public EventStatus Status { get; set; }
    public EventVisibility Visibility { get; set; }
    public Guid? EventTemplateId { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<EventChecklistItemDTO> ChecklistItems { get; set; } = new();
}
