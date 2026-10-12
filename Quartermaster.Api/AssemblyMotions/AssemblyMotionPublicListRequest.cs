using System;

namespace Quartermaster.Api.AssemblyMotions;

public class AssemblyMotionPublicListRequest : IPaginatedRequest {
    public Guid PortalId { get; set; }
    public Guid? TypeId { get; set; }
    public AssemblyMotionStatus? Status { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
