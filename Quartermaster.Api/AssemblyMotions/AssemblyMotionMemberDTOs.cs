using System;
using System.Collections.Generic;

namespace Quartermaster.Api.AssemblyMotions;

public class AssemblyMotionPortalContextDTO {
    public bool IsSignedIn { get; set; }
    public bool IsEligible { get; set; }
    public EligibilityProblem Problem { get; set; }
    public bool IsCommission { get; set; }
    public bool CanEditPortal { get; set; }
}

public class AssemblyMotionCollectingItemDTO : AssemblyMotionPublicListItemDTO {
    public int SubmitterCount { get; set; }
    public int MinSubmitters { get; set; }
    public DateTimeOffset EffectiveDeadline { get; set; }
}

public class AssemblyMotionCollectingListResponse : IPaginatedResponse<AssemblyMotionCollectingItemDTO> {
    public List<AssemblyMotionCollectingItemDTO> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class AssemblyMotionSubmitterDTO {
    public Guid MemberId { get; set; }
    public string DisplayName { get; set; } = "";
    public bool IsAuthor { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
    public int JoinedRevision { get; set; }
    public int? MemberNumber { get; set; }
}

public class AssemblyMotionDetailDTO : AssemblyMotionPublicDTO {
    public string TextMarkdown { get; set; } = "";
    public string ReasonMarkdown { get; set; } = "";
    public int Revision { get; set; }
    public bool IsCompetingMotion { get; set; }
    public int MinSubmitters { get; set; }
    public DateTimeOffset EffectiveDeadline { get; set; }
    public List<AssemblyMotionSubmitterDTO> Submitters { get; set; } = [];
    public bool IsAuthor { get; set; }
    public bool IsSubmitter { get; set; }
    public bool CanEdit { get; set; }
    public bool CanRelease { get; set; }
    public bool CanJoin { get; set; }
    public bool CanLeave { get; set; }
    public bool CanWithdraw { get; set; }
    public bool CanDelete { get; set; }
}

public class AssemblyMotionMineItemDTO {
    public Guid Id { get; set; }
    public string? DisplayNumber { get; set; }
    public string Title { get; set; } = "";
    public AssemblyMotionStatus Status { get; set; }
    public bool IsAuthor { get; set; }
    public int OpenInquiryCount { get; set; }
}
