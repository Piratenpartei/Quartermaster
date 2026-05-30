using System;
using System.Collections.Generic;

namespace Quartermaster.Api.Meetings;

public class MeetingDetailDTO {
    public Guid Id { get; set; }
    public Guid ChapterId { get; set; }
    public string ChapterName { get; set; } = "";
    public string Title { get; set; } = "";
    public DateOnly? MeetingDate { get; set; }
    public MeetingStatus Status { get; set; }
    public MeetingVisibility Visibility { get; set; }
    public string? Location { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? ArchivedPdfPath { get; set; }
    public List<AgendaItemDTO> AgendaItems { get; set; } = new();
}
