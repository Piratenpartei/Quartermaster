using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Server.AssemblyAssistant.Session;

namespace Quartermaster.Server.Tests.AssemblyAssistant.Fakes;

public class RecordingAssemblyPublisher : IAssemblyPublisher {
    private readonly Lock _lock = new();
    private readonly List<AssemblySnapshotDTO> _snapshots = [];

    public int EndedCount { get; private set; }

    public int Count {
        get {
            using (_lock.EnterScope()) {
                return _snapshots.Count;
            }
        }
    }

    public Task PublishAsync(Guid eventId, AssemblySnapshotDTO snapshot, AssemblyOverlayDTO overlay) {
        using (_lock.EnterScope()) {
            _snapshots.Add(snapshot);
        }
        return Task.CompletedTask;
    }

    public Task PublishEndedAsync(Guid eventId) {
        EndedCount++;
        return Task.CompletedTask;
    }
}
