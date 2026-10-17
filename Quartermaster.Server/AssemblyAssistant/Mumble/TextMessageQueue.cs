using System;
using System.Collections.Generic;

namespace Quartermaster.Server.AssemblyAssistant.Mumble;

/// <summary>
/// The server drops text messages beyond its rate limit (default 1 per second, burst 5) without telling the sender,
/// so messages leave at most once per second.
/// </summary>
public class TextMessageQueue {
    private readonly TimeSpan _interval;
    private readonly Queue<(uint Session, string Message)> _pending = new();
    private DateTime? lastSentAt;

    public int Count => _pending.Count;

    public TextMessageQueue() : this(TimeSpan.FromSeconds(1)) { }

    public TextMessageQueue(TimeSpan interval) {
        _interval = interval;
    }

    public void Enqueue(uint session, string message) {
        _pending.Enqueue((session, message));
    }

    public (uint Session, string Message)? DequeueDue(DateTime nowUtc) {
        if (_pending.Count == 0)
            return null;
        if (lastSentAt != null && nowUtc - lastSentAt.Value < _interval)
            return null;

        lastSentAt = nowUtc;
        return _pending.Dequeue();
    }

    public void Clear() {
        _pending.Clear();
    }
}
