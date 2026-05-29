using System;

namespace Quartermaster.Api.Motions;

public class MotionVoteRequest {
    public Guid MotionId { get; set; }

    /// <summary>The officer (member) whose vote is being recorded.</summary>
    public Guid MemberId { get; set; }
    public VoteType Vote { get; set; }
}
