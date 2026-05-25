using System;
using System.Collections.Generic;

namespace Quartermaster.Api.Events;

public class EventTemplateCreateRequest {
    public Guid EventId { get; set; }
    public string Name { get; set; } = "";
    public List<EventTemplateVariableDTO> Variables { get; set; } = [];
}
