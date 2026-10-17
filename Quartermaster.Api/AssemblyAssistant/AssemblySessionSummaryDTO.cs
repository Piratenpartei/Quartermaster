using System;

namespace Quartermaster.Api.AssemblyAssistant;

public class AssemblySessionSummaryDTO {
    public Guid Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public DateTime? LogDeletedAt { get; set; }
    public int EntryCount { get; set; }
}
