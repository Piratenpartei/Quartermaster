using Quartermaster.Api;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Members;
using Quartermaster.Data.Motions;

namespace Quartermaster.Server.Authentication;

/// <summary>Who may open the detail pages that link to each other. The detail endpoints and their buttons to linked pages share these rules.</summary>
public class DetailPageAccess {
    private readonly PermissionContext _perms;

    public DetailPageAccess(PermissionContext perms) {
        _perms = perms;
    }

    public bool CanViewMotion(Motion motion) {
        if (motion.IsPublic)
            return true;
        return _perms.Has(motion.ChapterId, PermissionIdentifier.ViewMotions);
    }

    public bool CanViewApplication(MembershipApplication application) {
        if (application.ChapterId.HasValue)
            return _perms.Has(application.ChapterId.Value, PermissionIdentifier.ViewApplications);
        return _perms.HasGlobal(PermissionIdentifier.ViewApplications);
    }

    /// <summary>The deciding chapter owns the selection; older rows without one belong to their application's chapter.</summary>
    public bool CanViewDueSelection(DueSelection dueSelection, MembershipApplication? application) {
        var chapterId = dueSelection.ChapterId ?? application?.ChapterId;
        if (chapterId.HasValue)
            return _perms.Has(chapterId.Value, PermissionIdentifier.ViewDueSelections);
        return _perms.HasGlobal(PermissionIdentifier.ViewDueSelections);
    }

    public bool CanViewMember(Member member) {
        if (member.ChapterId.HasValue)
            return _perms.Has(member.ChapterId.Value, PermissionIdentifier.ViewAllMembers, PermissionIdentifier.ViewMembers);
        return _perms.HasGlobal(PermissionIdentifier.ViewAllMembers);
    }

    public bool CanViewUsers() {
        return _perms.HasGlobal(PermissionIdentifier.ViewUsers);
    }
}
