using System;
using LinqToDB.Mapping;
using Quartermaster.Api;
using Quartermaster.Api.Events;

namespace Quartermaster.Data.Events;

[Table(TableName, IsColumnAttributeRequired = false)]
public class Event {
    public const string TableName = "Events";
    public const int MeetingUrlMaxLength = 500;

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChapterId { get; set; }
    public string InternalName { get; set; } = "";
    public string PublicName { get; set; } = "";
    public string? Description { get; set; }
    public DateTime? EventDate { get; set; }

    /// <summary>Berlin wall-clock time on <see cref="EventDate"/>; events are always local, so no instant is stored.</summary>
    public TimeSpan? StartTime { get; set; }
    public string? MeetingUrl { get; set; }
    public EventStatus Status { get; set; } = EventStatus.Draft;
    public EventVisibility Visibility { get; set; } = EventVisibility.Private;
    public Guid? EventTemplateId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public static Event FromCreateRequest(EventCreateRequest req, DateTime nowUtc) => new() {
        ChapterId = req.ChapterId,
        InternalName = req.InternalName,
        PublicName = req.PublicName,
        Description = req.Description,
        EventDate = req.EventDate.ToStorage(),
        StartTime = req.StartTime.ToStorage(),
        MeetingUrl = string.IsNullOrWhiteSpace(req.MeetingUrl) ? null : req.MeetingUrl.Trim(),
        Visibility = req.Visibility,
        CreatedAt = nowUtc
    };

    public EventDetailDTO ToDetailDto(string chapterName) => new() {
        Id = Id,
        ChapterId = ChapterId,
        ChapterName = chapterName,
        InternalName = InternalName,
        PublicName = PublicName,
        Description = Description,
        EventDate = EventDate.ToDtoDate(),
        StartTime = StartTime.ToDtoTime(),
        MeetingUrl = MeetingUrl,
        Status = Status,
        Visibility = Visibility,
        EventTemplateId = EventTemplateId,
        CreatedAt = CreatedAt.ToDtoUtc()
    };
}
