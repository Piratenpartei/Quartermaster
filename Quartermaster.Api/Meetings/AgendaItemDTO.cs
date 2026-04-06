using System;

namespace Quartermaster.Api.Meetings;

public class AgendaItemDTO {
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public int SortOrder { get; set; }
    public string Title { get; set; } = "";
    public AgendaItemType ItemType { get; set; }
    public Guid? MotionId { get; set; }
    public string? MotionTitle { get; set; }
    public int? MotionApprovalStatus { get; set; }
    public int MotionVoteApproveCount { get; set; }
    public int MotionVoteDenyCount { get; set; }
    public int MotionVoteAbstainCount { get; set; }
    public string? Notes { get; set; }
    public string? Resolution { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public enum AgendaItemType {
    Discussion = 0,
    Motion = 1,
    Protocol = 2,
    Break = 3,
    Information = 4
}
