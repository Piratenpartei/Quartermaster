using System.Collections.Generic;

namespace Quartermaster.Api.Events;

public class EventSearchResponse : IPaginatedResponse<EventDTO> {
    public List<EventDTO> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
