using System;

namespace Quartermaster.Api.Motions;

public class MotionVoteDTO {
    public Guid UserId { get; set; }
    public string UserName { get; set; } = "";
    public string OfficerRole { get; set; } = "";
    public int Vote { get; set; }
    public DateTime VotedAt { get; set; }
}
