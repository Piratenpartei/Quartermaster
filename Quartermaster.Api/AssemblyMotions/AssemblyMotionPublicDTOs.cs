using System;
using System.Collections.Generic;

namespace Quartermaster.Api.AssemblyMotions;

public class AssemblyMotionPublicListItemDTO {
    public Guid Id { get; set; }
    public Guid TypeId { get; set; }
    public string TypeCode { get; set; } = "";
    public string TypeName { get; set; } = "";
    public string DisplayNumber { get; set; } = "";
    public string Title { get; set; } = "";
    public AssemblyMotionStatus Status { get; set; }
    public bool IsInRevision { get; set; }
    public AssemblyMotionResult Result { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
}

public class AssemblyMotionPublicDTO : AssemblyMotionPublicListItemDTO {
    public Guid PortalId { get; set; }
    public Guid EventId { get; set; }
    public string? Section { get; set; }
    public string TextHtml { get; set; } = "";
    public string ReasonHtml { get; set; } = "";
    public string? Summary { get; set; }
    public string? Keywords { get; set; }
    public string? PriorDiscussion { get; set; }
    public string? CheckNote { get; set; }
    public RequiredMajority RequiredMajority { get; set; }
    public List<AssemblyMotionCompetitionPublicDTO> Competitions { get; set; } = [];
    public int? UpvoteCount { get; set; }
}

public class AssemblyMotionCompetitionPublicDTO {
    public Guid Id { get; set; }
    public Guid OtherMotionId { get; set; }
    public string DisplayNumber { get; set; } = "";
    public string Title { get; set; } = "";
}

public class AssemblyMotionPublicListResponse : IPaginatedResponse<AssemblyMotionPublicListItemDTO> {
    public List<AssemblyMotionPublicListItemDTO> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
