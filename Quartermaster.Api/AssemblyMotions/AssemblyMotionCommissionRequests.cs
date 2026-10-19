using System;

namespace Quartermaster.Api.AssemblyMotions;

public class AssemblyMotionDashboardRequest : IPaginatedRequest {
    public Guid PortalId { get; set; }
    public Guid? TypeId { get; set; }
    public AssemblyMotionStatus? Status { get; set; }
    public bool? HasOpenInquiry { get; set; }
    public WikiSyncState? WikiSyncState { get; set; }
    public bool? WikiFailed { get; set; }
    public ForumSyncState? ForumSyncState { get; set; }
    public bool? ForumFailed { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public class AssemblyMotionCheckRequest {
    public Guid Id { get; set; }
    public AssemblyMotionStatus Status { get; set; }
    public string? CheckNote { get; set; }
}

public class AssemblyMotionCommissionEditRequest : AssemblyMotionSaveRequest {
    public string ChangeNote { get; set; } = "";
}
