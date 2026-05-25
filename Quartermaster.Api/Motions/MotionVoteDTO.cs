using System;

namespace Quartermaster.Api.Motions;

public class MotionVoteDTO {
    public Guid UserId { get; set; }
    public string UserName { get; set; } = "";
    public string OfficerRole { get; set; } = "";
    public VoteType Vote { get; set; }
    public DateTime VotedAt { get; set; }
}
