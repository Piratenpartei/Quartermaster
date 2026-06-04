using System;
using LinqToDB.Mapping;
using Quartermaster.Api.ChapterMails;

namespace Quartermaster.Data.ChapterMails;

[Table(TableName, IsColumnAttributeRequired = false)]
public class ChapterMail {
    public const string TableName = "ChapterMails";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ChapterId { get; set; }
    public string Title { get; set; } = "";
    public string? Subject { get; set; }
    public ChapterMailTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public bool IncludeDescendants { get; set; } = true;

    public Guid? TemplateId { get; set; }
    public string? Content { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }

    public DateTime? SentAt { get; set; }
    public Guid? SentByUserId { get; set; }
    public int? RecipientCount { get; set; }

    public DateTime? DeletedAt { get; set; }

    [NotColumn]
    public bool IsSent => SentAt.HasValue;

    [NotColumn]
    public bool IsArchived => DeletedAt.HasValue;
}
