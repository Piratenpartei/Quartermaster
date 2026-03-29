using System;

namespace Quartermaster.Api.Motions;

public class MotionVoteRequest {
    public Guid MotionId { get; set; }
    public Guid UserId { get; set; }
    public int Vote { get; set; }
}
