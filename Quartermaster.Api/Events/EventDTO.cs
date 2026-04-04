using System;

namespace Quartermaster.Api.Events;

public class EventDTO {
    public Guid Id { get; set; }
    public Guid ChapterId { get; set; }
    public string ChapterName { get; set; } = "";
    public string PublicName { get; set; } = "";
    public DateTime? EventDate { get; set; }
    public EventStatus Status { get; set; }
    public EventVisibility Visibility { get; set; }
    public int ChecklistTotal { get; set; }
    public int ChecklistCompleted { get; set; }
    public DateTime CreatedAt { get; set; }
}
