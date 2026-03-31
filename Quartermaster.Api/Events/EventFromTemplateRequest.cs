using System;
using System.Collections.Generic;

namespace Quartermaster.Api.Events;

public class EventFromTemplateRequest {
    public Guid TemplateId { get; set; }
    public Guid ChapterId { get; set; }
    public DateTime? EventDate { get; set; }
    public Dictionary<string, string> VariableValues { get; set; } = new();
}
