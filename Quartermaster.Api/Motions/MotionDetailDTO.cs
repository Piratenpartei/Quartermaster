using System;
using System.Collections.Generic;
using Quartermaster.Api.Chapters;

namespace Quartermaster.Api.Motions;

public class MotionDetailDTO {
    public Guid Id { get; set; }
    public Guid ChapterId { get; set; }
    public string ChapterName { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public string AuthorEmail { get; set; } = "";
    public string Title { get; set; } = "";

    /// <summary>Rendered HTML for display.</summary>
    public string Text { get; set; } = "";

    /// <summary>Markdown source - populated for officers who may edit; <c>null</c> for public/read-only callers.</summary>
    public string? TextMarkdown { get; set; }

    public bool IsPublic { get; set; }
    public Guid? LinkedMembershipApplicationId { get; set; }
    public Guid? LinkedDueSelectionId { get; set; }
    public MotionApprovalStatus ApprovalStatus { get; set; }
    public bool IsRealized { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public List<MotionVoteDTO> Votes { get; set; } = [];
    public List<MotionVoteDTO> Officers { get; set; } = [];
    public int TotalOfficers { get; set; }

    /// <summary>The marked chapter this motion is decided for. Only filled for signed-in callers.</summary>
    public Guid? OnBehalfOfChapterId { get; set; }
    public string? OnBehalfOfChapterName { get; set; }
    public ChapterMark? OnBehalfOfReason { get; set; }

    /// <summary>The replacement motion this one was handed over to. Only filled for signed-in callers.</summary>
    public Guid? HandedOverToMotionId { get; set; }
    public string? HandedOverToChapterName { get; set; }

    /// <summary>Why this motion was handed over. Only filled for signed-in callers, like the fields below.</summary>
    public MotionHandoverReason? HandoverReason { get; set; }

    /// <summary>Set on a replacement motion: the chapter it was handed over from, the reason and when the application came in.</summary>
    public string? HandedOverFromChapterName { get; set; }
    public MotionHandoverReason? HandedOverFromReason { get; set; }
    public DateTimeOffset? ApplicationSubmittedAt { get; set; }

    /// <summary>When and to which chapter a waiting application motion moves next if nobody decides before.</summary>
    public DateTimeOffset? NextTakeOverAt { get; set; }
    public string? NextTakeOverChapterName { get; set; }

    /// <summary>True when the caller may take this reduced due selection over to the federal chapter.</summary>
    public bool CanTakeOver { get; set; }
}
