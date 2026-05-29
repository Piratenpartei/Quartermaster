using System;

namespace Quartermaster.Api.Motions;

public class MotionVoteDTO {
    public Guid MemberId { get; set; }
    public string MemberName { get; set; } = "";
    public string OfficerRole { get; set; } = "";
    public VoteType Vote { get; set; }
    public DateTime VotedAt { get; set; }

    /// <summary>The user who recorded the vote (officer themselves or a chair/admin). Empty on officer rows with no vote yet.</summary>
    public Guid CastByUserId { get; set; }
}
