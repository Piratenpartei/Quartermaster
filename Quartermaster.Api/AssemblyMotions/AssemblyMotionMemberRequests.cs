using System;

namespace Quartermaster.Api.AssemblyMotions;

public class AssemblyMotionSaveRequest {
    public Guid PortalId { get; set; }
    public Guid Id { get; set; }
    public Guid TypeId { get; set; }
    public string Title { get; set; } = "";
    public string? Section { get; set; }
    public string TextMarkdown { get; set; } = "";
    public string ReasonMarkdown { get; set; } = "";
    public string? Summary { get; set; }
    public string? Keywords { get; set; }
    public string? PriorDiscussion { get; set; }
    public int BaseRevision { get; set; }
}

public class AssemblyMotionIdRequest {
    public Guid Id { get; set; }
}

public class AssemblyMotionCompetitionDeclareRequest {
    public Guid MotionId { get; set; }
    public Guid OtherMotionId { get; set; }
}

public class AssemblyMotionCompetitionIdRequest {
    public Guid Id { get; set; }
}

public class AssemblyMotionCompetingFlagRequest {
    public Guid Id { get; set; }
    public bool Value { get; set; }
}

public class AssemblyMotionPortalIdRequest {
    public Guid PortalId { get; set; }
}

public class AssemblyMotionCollectingListRequest : IPaginatedRequest {
    public Guid PortalId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
