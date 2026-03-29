using System;
using System.Collections.Generic;

namespace Quartermaster.Api.Motions;

public class MotionDetailDTO {
    public Guid Id { get; set; }
    public Guid ChapterId { get; set; }
    public string ChapterName { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public string AuthorEMail { get; set; } = "";
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public bool IsPublic { get; set; }
    public Guid? LinkedMembershipApplicationId { get; set; }
    public Guid? LinkedDueSelectionId { get; set; }
    public int ApprovalStatus { get; set; }
    public bool IsRealized { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public List<MotionVoteDTO> Votes { get; set; } = [];
    public List<MotionVoteDTO> Officers { get; set; } = [];
    public int TotalOfficers { get; set; }
}
