using System.Linq;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.AssemblyMotions;

public class AssemblyMotionPortalSettingsBuilder {
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly EventRepository _events;

    public AssemblyMotionPortalSettingsBuilder(AssemblyMotionPortalRepository portals, EventRepository events) {
        _portals = portals;
        _events = events;
    }

    public AssemblyMotionPortalSettingsDTO Build(AssemblyMotionPortal portal, Event ev) {
        var types = _portals.GetTypes(portal.Id);
        var typeIdsWithMotions = types
            .Where(t => _portals.TypeHasMotions(t.Id))
            .Select(t => t.Id)
            .ToHashSet();

        return portal.ToSettingsDto(ev.PublicName, ev.EventDate, types, typeIdsWithMotions);
    }
}
