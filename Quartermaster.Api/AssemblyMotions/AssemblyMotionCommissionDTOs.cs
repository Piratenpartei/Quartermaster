using System;
using System.Collections.Generic;

namespace Quartermaster.Api.AssemblyMotions;

public class AssemblyMotionDashboardItemDTO {
    public Guid Id { get; set; }
    public string? DisplayNumber { get; set; }
    public string Title { get; set; } = "";
    public string TypeCode { get; set; } = "";
    public AssemblyMotionStatus Status { get; set; }
    public AssemblyMotionResult Result { get; set; }
    public int SubmitterCount { get; set; }
    public int MinSubmitters { get; set; }
    public int OpenInquiryCount { get; set; }
    public DateTimeOffset EffectiveDeadline { get; set; }
    public bool DeadlinePassed { get; set; }
    public WikiSyncState WikiSyncState { get; set; }
    public bool IsInRevision { get; set; }
    public int Revision { get; set; }
    public int? OpenSlidesPushedRevision { get; set; }
}

public class AssemblyMotionDashboardResponse : IPaginatedResponse<AssemblyMotionDashboardItemDTO> {
    public List<AssemblyMotionDashboardItemDTO> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int FailedWikiSyncCount { get; set; }
}

public class AssemblyMotionCommissionInfoDTO {
    public DateTimeOffset? CheckedAt { get; set; }
    public string? CheckedByName { get; set; }
    public WikiSyncState WikiSyncState { get; set; }
    public int? WikiSyncedRevision { get; set; }
    public DateTimeOffset? WikiLastSyncedAt { get; set; }
    public string? WikiLastError { get; set; }
    public int? OpenSlidesMotionId { get; set; }
    public int? OpenSlidesPushedRevision { get; set; }
    public DateTimeOffset? OpenSlidesPushedAt { get; set; }
    public string? OpenSlidesLastError { get; set; }
}

public class AssemblyMotionManagedPortalDTO {
    public Guid PortalId { get; set; }
    public Guid EventId { get; set; }
    public string EventName { get; set; } = "";
    public DateOnly? EventDate { get; set; }
    public string ChapterName { get; set; } = "";
}
