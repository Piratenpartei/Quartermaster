using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.Events;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.AssemblyMotions;

public static class AssemblyMotionSubscriptionRules {
    /// <summary>Null when the viewer may subscribe; event visibility is checked by the caller.</summary>
    public static (int StatusCode, string? ErrorKey)? Problem(AssemblyMotion motion, Event ev, bool viewerIsAuthor) {
        if (motion.DeletedAt != null || motion.Status == AssemblyMotionStatus.Draft)
            return (404, null);
        if (viewerIsAuthor)
            return (409, I18nKey.Error.AssemblyMotion.SubscriptionAuthor);
        if (ev.DeletedAt != null || ev.Status == EventStatus.Completed || ev.Status == EventStatus.Archived)
            return (409, I18nKey.Error.AssemblyMotion.SubscriptionClosed);
        return null;
    }
}
