using System;

namespace Quartermaster.Api.Events;

public class EventSearchRequest {
    public Guid? ChapterId { get; set; }
    public bool IncludeArchived { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
