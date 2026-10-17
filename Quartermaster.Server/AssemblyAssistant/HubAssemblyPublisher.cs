using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Server.AssemblyAssistant.Session;

namespace Quartermaster.Server.AssemblyAssistant;

public class HubAssemblyPublisher : IAssemblyPublisher {
    private readonly IHubContext<AssemblyHub> _hub;

    public HubAssemblyPublisher(IHubContext<AssemblyHub> hub) {
        _hub = hub;
    }

    public async Task PublishAsync(Guid eventId, AssemblySnapshotDTO snapshot, AssemblyOverlayDTO overlay) {
        await _hub.Clients.Group(AssemblyHub.ConsoleGroup(eventId)).SendAsync(AssemblyHubMethods.SnapshotChanged, snapshot);
        await _hub.Clients.Group(AssemblyHub.OverlayGroup(eventId)).SendAsync(AssemblyHubMethods.OverlayChanged, overlay);
    }

    public async Task PublishEndedAsync(Guid eventId) {
        await _hub.Clients.Group(AssemblyHub.ConsoleGroup(eventId)).SendAsync(AssemblyHubMethods.SessionEnded);
        await _hub.Clients.Group(AssemblyHub.OverlayGroup(eventId)).SendAsync(AssemblyHubMethods.OverlayChanged,
            new AssemblyOverlayDTO { ServerNowUtc = DateTime.UtcNow, Active = false });
    }
}
