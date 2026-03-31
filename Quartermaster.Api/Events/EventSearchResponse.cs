using System.Collections.Generic;

namespace Quartermaster.Api.Events;

public class EventSearchResponse {
    public List<EventDTO> Items { get; set; } = new();
    public int TotalCount { get; set; }
}
