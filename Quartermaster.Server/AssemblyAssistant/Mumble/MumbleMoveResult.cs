namespace Quartermaster.Server.AssemblyAssistant.Mumble;

public enum MumbleMoveOutcome {
    Moved = 0,
    PermissionDenied = 1,
    ChannelFull = 2,
    Timeout = 3,
    NotConnected = 4,
    UnknownUser = 5
}

public record MumbleMoveResult(MumbleMoveOutcome Outcome, string? Detail);
