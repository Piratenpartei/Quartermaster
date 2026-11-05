using System;

namespace Quartermaster.Api.Events;

public class EventSearchRequest : IPaginatedRequest {
    public Guid? ChapterId { get; set; }
    public bool IncludeArchived { get; set; }

    /// <summary>Lists the events the caller manages (view or edit rights on the chapter or a chapter above) instead of what a visitor may see.</summary>
    public bool Managed { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
