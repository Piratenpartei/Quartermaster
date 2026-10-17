using System;
using System.Collections.Generic;

namespace Quartermaster.Server.AssemblyAssistant.Mumble;

/// <summary>
/// The server drops a client that was silent for 30 s, so pings go out every 10 s; four unanswered pings mean the connection is dead.
/// </summary>
public record MumbleConnectionTimings(TimeSpan PingInterval, int MaxMissedPings, TimeSpan ConnectTimeout, TimeSpan MoveTimeout,
    TimeSpan TickInterval, TimeSpan TextInterval, IReadOnlyList<TimeSpan> ReconnectBackoff) {

    public static MumbleConnectionTimings Default { get; } = new(
        TimeSpan.FromSeconds(10),
        4,
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(3),
        TimeSpan.FromMilliseconds(250),
        TimeSpan.FromSeconds(1),
        [
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8),
            TimeSpan.FromSeconds(16), TimeSpan.FromSeconds(30)
        ]);
}
