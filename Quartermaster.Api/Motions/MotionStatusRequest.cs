using System;

namespace Quartermaster.Api.Motions;

public class MotionStatusRequest {
    public Guid MotionId { get; set; }
    public MotionApprovalStatus? ApprovalStatus { get; set; }
    public bool? IsRealized { get; set; }
    public bool? IsPublic { get; set; }
}
