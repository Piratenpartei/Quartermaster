using System;

namespace Quartermaster.Api.Motions;

public class MotionStatusRequest {
    public Guid MotionId { get; set; }
    public int? ApprovalStatus { get; set; }
    public bool? IsRealized { get; set; }
}
