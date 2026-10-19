using System;
using Quartermaster.Api;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.TravelExpenses;

public static class TravelExpenseRulePermissions {
    /// <summary>The global default needs the permission granted globally, a chapter's rules the permission on exactly that chapter.</summary>
    public static bool CanEdit(PermissionContext perms, Guid? chapterId) {
        if (chapterId == null)
            return perms.HasGlobal(PermissionIdentifier.EditTravelExpenseRules);

        return perms.Has(chapterId.Value, PermissionIdentifier.EditTravelExpenseRules);
    }
}
