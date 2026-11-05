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
    public DateOnly? EventDate { get; set; }
    public TimeOnly? StartTime { get; set; }
    public string? MeetingUrl { get; set; }
    public EventStatus Status { get; set; }
    public EventVisibility Visibility { get; set; }
    public Guid? EventTemplateId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<EventChecklistItemDTO> ChecklistItems { get; set; } = new();
    /// <summary>The caller may open the management page; only then are the internal name and the checklist filled.</summary>
    public bool CanManage { get; set; }
    public bool CanEdit { get; set; }
    public bool CanArchive { get; set; }
    public bool CanSaveAsTemplate { get; set; }
}
