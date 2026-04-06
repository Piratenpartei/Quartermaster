using System;

namespace Quartermaster.Api.Meetings;

public class MeetingDTO {
    public Guid Id { get; set; }
    public Guid ChapterId { get; set; }
    public string ChapterName { get; set; } = "";
    public string Title { get; set; } = "";
    public DateTime? MeetingDate { get; set; }
    public MeetingStatus Status { get; set; }
    public MeetingVisibility Visibility { get; set; }
    public string? Location { get; set; }
    public int AgendaItemCount { get; set; }
}

public enum MeetingStatus {
    Draft = 0,
    Scheduled = 1,
    InProgress = 2,
    Completed = 3,
    Archived = 4
}

public enum MeetingVisibility {
    Public = 0,
    Private = 1
}
