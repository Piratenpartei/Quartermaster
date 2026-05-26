using System;
using System.Collections.Generic;
using Quartermaster.Api.Motions;

namespace Quartermaster.Api.Meetings;

public class AgendaItemDTO {
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public int SortOrder { get; set; }
    public string Title { get; set; } = "";
    public AgendaItemType ItemType { get; set; }
    public Guid? MotionId { get; set; }
    public string? MotionTitle { get; set; }
    public MotionApprovalStatus? MotionApprovalStatus { get; set; }
    public int MotionVoteApproveCount { get; set; }
    public int MotionVoteDenyCount { get; set; }
    public int MotionVoteAbstainCount { get; set; }
    public string? Notes { get; set; }
    public string? Resolution { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    /// <summary>
    /// For Motion-type items: per-officer vote breakdown. For Presence-type items: officer attendance list.
    /// </summary>
    public List<AgendaItemOfficerVoteDTO>? OfficerVotes { get; set; }
}

/// <summary>
/// One row per chapter officer for vote display (motion items) or attendance (presence items).
/// </summary>
public class AgendaItemOfficerVoteDTO {
    public Guid UserId { get; set; }
    public string UserName { get; set; } = "";
    public string OfficerRole { get; set; } = "";
    public VoteType? Vote { get; set; }
    public bool IsPresent { get; set; }
}

public enum AgendaItemType {
    Discussion = 0,
    Motion = 1,
    Protocol = 2,
    Break = 3,
    Information = 4,
    Section = 5,
    Presence = 6
}
