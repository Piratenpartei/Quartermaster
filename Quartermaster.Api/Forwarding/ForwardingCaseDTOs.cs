using System;
using System.Collections.Generic;

namespace Quartermaster.Api.Forwarding;

/// <summary>A forwarding case in the failure list. Never carries the submitted data.</summary>
public class ForwardingCaseListItemDTO {
    public Guid Id { get; set; }
    public string Reference { get; set; } = "";
    public ForwardingCaseKind Kind { get; set; }
    public ForwardingCaseStatus Status { get; set; }
    public string ChapterName { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public List<ForwardingStepDTO> Steps { get; set; } = [];
}

public class ForwardingStepDTO {
    public ForwardingStepKind Kind { get; set; }
    public ForwardingPurpose Purpose { get; set; }
    public string TargetChapterName { get; set; } = "";
    public ForwardingStepState State { get; set; }
    public string? TicketUrl { get; set; }
    public string? LastError { get; set; }
    public int Attempts { get; set; }
}

public class ForwardingCaseListRequest : IPaginatedRequest {
    public ForwardingCaseStatus? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public class ForwardingCaseListResponse : IPaginatedResponse<ForwardingCaseListItemDTO> {
    public List<ForwardingCaseListItemDTO> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class ForwardingCaseRequest {
    public Guid Id { get; set; }
}
