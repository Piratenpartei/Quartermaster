using System;

namespace Quartermaster.Api.Events;

public class EventTemplateCreateRequest {
    public Guid EventId { get; set; }
    public string Name { get; set; } = "";
    public string Variables { get; set; } = "[]";
}
