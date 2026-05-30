using System;

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
}
