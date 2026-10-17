using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Server.AssemblyAssistant.Session;

/// <summary>Entries of members who left a request channel or lost their connection, kept for the grace period.</summary>
public class GraceStore {
    private readonly Dictionary<string, (QueueEntry Entry, DateTime LeftAt)> _parked = new();
    private readonly TimeSpan _grace;

    public GraceStore(TimeSpan grace) {
        _grace = grace;
    }

    public void Park(QueueEntry entry, DateTime nowUtc) {
        entry.MovePending = false;
        _parked[entry.Key] = (entry, nowUtc);
    }

    public QueueEntry? TryRestore(string key, DateTime nowUtc) {
        if (!_parked.Remove(key, out var parked))
            return null;

        return nowUtc - parked.LeftAt <= _grace ? parked.Entry : null;
    }

    public void Expire(DateTime nowUtc) {
        var expired = _parked
            .Where(p => nowUtc - p.Value.LeftAt > _grace)
            .Select(p => p.Key)
            .ToList();

        foreach (var key in expired)
            _parked.Remove(key);
    }
}
