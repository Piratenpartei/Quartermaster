using System;
using LinqToDB.Mapping;
using Quartermaster.Api;
using Quartermaster.Api.Motions;

namespace Quartermaster.Data.Motions;

[Table(TableName, IsColumnAttributeRequired = false)]
public class Motion {
    public const string TableName = "Motions";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ChapterId { get; set; }

    public string AuthorName { get; set; } = "";
    public string AuthorEmail { get; set; } = "";

    public string Title { get; set; } = "";

    /// <summary>Rendered, sanitized HTML for display. Derived from <see cref="TextMarkdown"/>.</summary>
    public string Text { get; set; } = "";

    /// <summary>Markdown source — the canonical edit surface and what's diffed in the audit log.</summary>
    public string TextMarkdown { get; set; } = "";

    public bool IsPublic { get; set; }

    public Guid? LinkedMembershipApplicationId { get; set; }
    public Guid? LinkedDueSelectionId { get; set; }

    public MotionApprovalStatus ApprovalStatus { get; set; }
    public bool IsRealized { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public static Motion Create(Guid chapterId, string authorName, string authorEmail, string title, string textMarkdown, string textHtml, DateTime nowUtc, Guid? linkedApplicationId = null, Guid? linkedDueSelectionId = null) => new() {
        ChapterId = chapterId,
        AuthorName = authorName,
        AuthorEmail = authorEmail,
        Title = title,
        Text = textHtml,
        TextMarkdown = textMarkdown,
        IsPublic = false,
        LinkedMembershipApplicationId = linkedApplicationId,
        LinkedDueSelectionId = linkedDueSelectionId,
        ApprovalStatus = MotionApprovalStatus.Pending,
        CreatedAt = nowUtc
    };

    public static Motion FromCreateRequest(MotionCreateRequest req, string textHtml, DateTime nowUtc)
        => Create(req.ChapterId, req.AuthorName, req.AuthorEmail, req.Title, req.Text, textHtml, nowUtc);

    public MotionDetailDTO ToDetailDto(string chapterName) => new() {
        Id = Id,
        ChapterId = ChapterId,
        ChapterName = chapterName,
        AuthorName = AuthorName,
        AuthorEmail = AuthorEmail,
        Title = Title,
        Text = Text,
        TextMarkdown = TextMarkdown,
        IsPublic = IsPublic,
        LinkedMembershipApplicationId = LinkedMembershipApplicationId,
        LinkedDueSelectionId = LinkedDueSelectionId,
        ApprovalStatus = ApprovalStatus,
        IsRealized = IsRealized,
        CreatedAt = CreatedAt.ToDtoUtc(),
        ResolvedAt = ResolvedAt.ToDtoUtc()
    };
}
