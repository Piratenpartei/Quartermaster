using System;
using System.Collections.Generic;

namespace Quartermaster.Api.Events;

public class RecipientPreviewRequest {
    public Guid EventId { get; set; }
    public EventChecklistItemConfigDTO Configuration { get; set; } = new();
}

public class RecipientPreviewResponse {
    public List<RecipientPreviewItem> Recipients { get; set; } = new();
}

public class RecipientPreviewItem {
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Reason { get; set; } = "";
}
