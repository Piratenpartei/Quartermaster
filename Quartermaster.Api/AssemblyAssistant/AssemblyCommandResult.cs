namespace Quartermaster.Api.AssemblyAssistant;

public class AssemblyCommandResult {
    public bool Accepted { get; set; }
    public string? MessageKey { get; set; }

    public static AssemblyCommandResult Ok() {
        return new AssemblyCommandResult { Accepted = true };
    }

    public static AssemblyCommandResult Rejected(string messageKey) {
        return new AssemblyCommandResult { Accepted = false, MessageKey = messageKey };
    }
}
