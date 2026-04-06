using System;
using LinqToDB.Mapping;
using Quartermaster.Api.Meetings;

namespace Quartermaster.Data.Meetings;

[Table(TableName, IsColumnAttributeRequired = false)]
public class Meeting {
    public const string TableName = "Meetings";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ChapterId { get; set; }
    public string Title { get; set; } = "";
    public DateTime? MeetingDate { get; set; }
    public string? Location { get; set; }
    public string? Description { get; set; }
    public MeetingStatus Status { get; set; } = MeetingStatus.Draft;
    public MeetingVisibility Visibility { get; set; } = MeetingVisibility.Private;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ArchivedPdfPath { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
