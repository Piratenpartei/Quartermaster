using System.Collections.Generic;
using System.Linq;
using Quartermaster.Api.AssemblyAssistant;

namespace Quartermaster.Server.AssemblyAssistant.Session;

/// <summary>Requests of one request channel in order of entry; a restored entry keeps its original entry time and so its place.</summary>
public class SpeakerQueue {
    private readonly List<QueueEntry> _entries = [];

    public QueueKind Kind { get; }
    public IReadOnlyList<QueueEntry> Entries => _entries;

    public SpeakerQueue(QueueKind kind) {
        Kind = kind;
    }

    public void Add(QueueEntry entry) {
        var index = _entries.FindIndex(e => e.EnteredAt > entry.EnteredAt || (e.EnteredAt == entry.EnteredAt && e.Sequence > entry.Sequence));
        if (index < 0) {
            _entries.Add(entry);
        } else {
            _entries.Insert(index, entry);
        }
    }

    public bool Remove(QueueEntry entry) {
        return _entries.Remove(entry);
    }

    public QueueEntry? FindByKey(string key) {
        return _entries.FirstOrDefault(e => e.Key == key);
    }

    public QueueEntry? FindBySession(uint session) {
        return _entries.FirstOrDefault(e => e.Session == session);
    }

    public QueueEntry? FirstSuggestable() {
        return _entries.FirstOrDefault(e => e.IsSuggestable());
    }
}
