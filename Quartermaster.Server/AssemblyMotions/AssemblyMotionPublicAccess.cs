using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Events;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.AssemblyMotions;

public class AssemblyMotionPublicAccess {
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly EventRepository _events;
    private readonly PermissionContext _perms;

    public AssemblyMotionPublicAccess(AssemblyMotionPortalRepository portals, EventRepository events, PermissionContext perms) {
        _portals = portals;
        _events = events;
        _perms = perms;
    }

    /// <summary>Returns the motion's portal when the current viewer may see the motion publicly, otherwise null.</summary>
    public AssemblyMotionPortal? GetPortal(AssemblyMotion motion) {
        if (!AssemblyMotionVisibility.IsPublic(motion))
            return null;

        var portal = _portals.Get(motion.PortalId);
        if (portal == null)
            return null;

        var ev = _events.Get(portal.EventId);
        if (ev == null)
            return null;

        return AssemblyMotionEventVisibility.IsVisible(ev, _perms) ? portal : null;
    }
}
