using Quartermaster.Api;
using Quartermaster.Data.Events;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.AssemblyMotions;

public class AssemblyMotionAccess {
    private readonly PermissionContext _perms;

    public AssemblyMotionAccess(PermissionContext perms) {
        _perms = perms;
    }

    public bool IsCommission(Event ev) {
        return _perms.Has(ev.ChapterId, PermissionIdentifier.ManageAssemblyMotions);
    }

    public bool CanEditPortal(Event ev) {
        return _perms.Has(ev.ChapterId, PermissionIdentifier.EditEvents);
    }
}
