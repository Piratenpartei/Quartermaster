namespace Quartermaster.Api.AssemblyAssistant;

public class AssemblyConsoleJoinDTO {
    public bool Allowed { get; set; }
    public bool CanControl { get; set; }
    public string? EventName { get; set; }

    /// <summary>Null while no session runs for the event.</summary>
    public AssemblySnapshotDTO? Snapshot { get; set; }
}
