using Quartermaster.Api;
using Quartermaster.Api.Events;
using Quartermaster.Data.Events;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.AssemblyMotions;

public static class AssemblyMotionEventVisibility {
    public static bool IsVisible(Event ev, PermissionContext perms) {
        if (ev.Visibility == EventVisibility.Private)
            return perms.UserId != null && perms.Has(ev.ChapterId, PermissionIdentifier.ViewEvents);
        if (ev.Visibility == EventVisibility.MembersOnly)
            return perms.UserId != null;
        return true;
    }
}
