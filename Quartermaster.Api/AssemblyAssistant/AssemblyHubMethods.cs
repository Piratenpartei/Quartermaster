namespace Quartermaster.Api.AssemblyAssistant;

/// <summary>Names of the client events pushed by the assembly hub at /hubs/assembly.</summary>
public static class AssemblyHubMethods {
    public const string HubPath = "/hubs/assembly";
    public const string SnapshotChanged = "AssemblySnapshotChanged";
    public const string OverlayChanged = "AssemblyOverlayChanged";
    public const string SessionEnded = "AssemblySessionEnded";
}
