using System;
using LinqToDB.Mapping;
using Quartermaster.Api.Motions;

namespace Quartermaster.Data.Motions;

[Table(TableName, IsColumnAttributeRequired = false)]
public class MotionVote {
    public const string TableName = "MotionVotes";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MotionId { get; set; }

    /// <summary>The officer (member) whose vote this is — a motion is decided by officers, not login accounts.</summary>
    public Guid MemberId { get; set; }

    /// <summary>The user who recorded the vote (the officer themselves, or a chair/admin acting for them). Always set.</summary>
    public Guid CastByUserId { get; set; }

    public VoteType Vote { get; set; }
    public DateTime VotedAt { get; set; }
    /// <summary>
    /// Set when this vote was cast as part of a meeting's agenda-item voting.
    /// Null for async votes outside a meeting context.
    /// </summary>
    public Guid? MeetingId { get; set; }
}
