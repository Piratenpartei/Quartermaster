using System;
using Quartermaster.Api.Chapters;

namespace Quartermaster.Api.Motions;

public class MotionDTO {
    public Guid Id { get; set; }
    public Guid ChapterId { get; set; }
    public string ChapterName { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public string Title { get; set; } = "";
    public bool IsPublic { get; set; }
    public MotionApprovalStatus ApprovalStatus { get; set; }
    public bool IsRealized { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>The marked chapter this motion is decided for. Only filled for signed-in callers.</summary>
    public Guid? OnBehalfOfChapterId { get; set; }
    public string? OnBehalfOfChapterName { get; set; }
    public ChapterMark? OnBehalfOfReason { get; set; }

    /// <summary>The replacement motion this one was handed over to. Only filled for signed-in callers.</summary>
    public Guid? HandedOverToMotionId { get; set; }
    public string? HandedOverToChapterName { get; set; }

    /// <summary>Why this motion was handed over. Only filled for signed-in callers.</summary>
    public MotionHandoverReason? HandoverReason { get; set; }
}
