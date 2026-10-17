using System;
using Quartermaster.Api;
using Quartermaster.Data.Events;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.AssemblyAssistant.Endpoints;

/// <summary>Shared check for the session endpoints. Returns the HTTP status to send, or null when the request may proceed.</summary>
public static class AssemblySessionGuard {
    public static int? Check(PermissionContext perms, EventRepository events, Guid eventId, string permission) {
        if (perms.UserId == null)
            return 401;

        var evt = events.Get(eventId);
        if (evt == null)
            return 404;

        return perms.Has(evt.ChapterId, permission) ? null : 403;
    }

    /// <summary>Reading (console, log export) is allowed with assembly_view, and with assembly_chair, which implies it.</summary>
    public static int? CheckView(PermissionContext perms, EventRepository events, Guid eventId) {
        var status = Check(perms, events, eventId, PermissionIdentifier.AssemblyView);
        if (status != 403)
            return status;

        return Check(perms, events, eventId, PermissionIdentifier.AssemblyChair);
    }
}
