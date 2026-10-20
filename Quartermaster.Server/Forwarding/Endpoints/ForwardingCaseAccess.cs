using System.Collections.Generic;
using Quartermaster.Api;
using Quartermaster.Api.Forwarding;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Forwarding.Endpoints;

/// <summary>Who may see and handle forwarding cases: application cases need the global ProcessApplications permission, due selection cases ProcessDueSelections.</summary>
public static class ForwardingCaseAccess {
    public static List<ForwardingCaseKind> VisibleKinds(PermissionContext perms) {
        var kinds = new List<ForwardingCaseKind>();
        if (perms.HasGlobal(PermissionIdentifier.ProcessApplications))
            kinds.Add(ForwardingCaseKind.Application);
        if (perms.HasGlobal(PermissionIdentifier.ProcessDueSelections))
            kinds.Add(ForwardingCaseKind.DueSelection);
        return kinds;
    }
}
