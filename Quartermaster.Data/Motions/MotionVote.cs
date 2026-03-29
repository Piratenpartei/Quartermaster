using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.Motions;

[Table(TableName, IsColumnAttributeRequired = false)]
public class MotionVote {
    public const string TableName = "MotionVotes";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MotionId { get; set; }
    public Guid UserId { get; set; }
    public VoteType Vote { get; set; }
    public DateTime VotedAt { get; set; }
}

public enum VoteType {
    Approve,
    Deny,
    Abstain
}
