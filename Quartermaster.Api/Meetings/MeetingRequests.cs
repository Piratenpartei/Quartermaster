using System;

namespace Quartermaster.Api.Meetings;

public class MeetingCreateRequest {
    public Guid ChapterId { get; set; }
    public string Title { get; set; } = "";
    public MeetingVisibility Visibility { get; set; } = MeetingVisibility.Private;
    public DateTime? MeetingDate { get; set; }
    public string? Location { get; set; }
    public string? Description { get; set; }
}

public class MeetingUpdateRequest {
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public MeetingVisibility Visibility { get; set; } = MeetingVisibility.Private;
    public DateTime? MeetingDate { get; set; }
    public string? Location { get; set; }
    public string? Description { get; set; }
}

public class MeetingStatusUpdateRequest {
    public Guid Id { get; set; }
    public MeetingStatus Status { get; set; }
}

public class MeetingListRequest : IPaginatedRequest {
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public Guid? ChapterId { get; set; }
    public MeetingStatus? Status { get; set; }
    public MeetingVisibility? Visibility { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
}

public class MeetingListResponse {
    public System.Collections.Generic.List<MeetingDTO> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class AgendaItemCreateRequest {
    public Guid MeetingId { get; set; }
    public Guid? ParentId { get; set; }
    public string Title { get; set; } = "";
    public AgendaItemType ItemType { get; set; }
    public Guid? MotionId { get; set; }
}

public class AgendaItemUpdateRequest {
    public Guid MeetingId { get; set; }
    public Guid ItemId { get; set; }
    public string Title { get; set; } = "";
    public AgendaItemType ItemType { get; set; }
    public Guid? MotionId { get; set; }
    public string? Notes { get; set; }
    public string? Resolution { get; set; }
}

public class AgendaItemNotesRequest {
    public Guid MeetingId { get; set; }
    public Guid ItemId { get; set; }
    public string? Notes { get; set; }
}

public class AgendaItemReorderRequest {
    public Guid MeetingId { get; set; }
    public Guid ItemId { get; set; }
    public int Direction { get; set; }
}

public class AgendaItemMoveRequest {
    public Guid MeetingId { get; set; }
    public Guid ItemId { get; set; }
    public Guid? NewParentId { get; set; }
}

public class AgendaItemVoteRequest {
    public Guid MeetingId { get; set; }
    public Guid ItemId { get; set; }
    public Guid UserId { get; set; }
    public int Vote { get; set; } // VoteType enum value
}
