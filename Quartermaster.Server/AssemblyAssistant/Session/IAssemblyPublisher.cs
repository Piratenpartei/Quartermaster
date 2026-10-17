using System;
using System.Threading.Tasks;
using Quartermaster.Api.AssemblyAssistant;

namespace Quartermaster.Server.AssemblyAssistant.Session;

/// <summary>Pushes session state to the console and overlay clients of an event.</summary>
public interface IAssemblyPublisher {
    Task PublishAsync(Guid eventId, AssemblySnapshotDTO snapshot, AssemblyOverlayDTO overlay);

    Task PublishEndedAsync(Guid eventId);
}
