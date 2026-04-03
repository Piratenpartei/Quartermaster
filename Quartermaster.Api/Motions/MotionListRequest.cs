using System;

namespace Quartermaster.Api.Motions;

public class MotionListRequest : IPaginatedRequest {
    public Guid? ChapterId { get; set; }
    public int? ApprovalStatus { get; set; }
    public bool IncludeNonPublic { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
