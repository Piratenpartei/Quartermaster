using System;
using System.Collections.Generic;

namespace Quartermaster.Server.AssemblyAssistant.OpenSlides;

/// <summary>The autoupdate service sends an empty heartbeat every 5 minutes, so 6 minutes of silence mean a dead stream.</summary>
public record OpenSlidesAutoupdateTimings(TimeSpan SilenceTimeout, IReadOnlyList<TimeSpan> ReconnectBackoff) {
    public static OpenSlidesAutoupdateTimings Default { get; } = new(
        TimeSpan.FromMinutes(6),
        [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)]);
}
