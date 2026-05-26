using System;
using Quartermaster.Api;
using Quartermaster.Api.Meetings;
using Quartermaster.Data.Meetings;
using Quartermaster.Data.Roles;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Meetings;

/// <summary>
/// Encapsulates the view-access rule for meetings:
/// - Draft: only users with meeting permissions on the chapter (officers, delegates,
///   or anyone with ViewMeetings/EditMeetings/CreateMeetings on the chapter).
/// - Public (non-draft): visible to everyone (including anonymous).
/// - Private (non-draft): direct officer OR delegate role on the meeting's chapter.
/// </summary>
public static class MeetingAccessHelper {
    public static bool CanUserViewMeeting(
        Guid? userId, Meeting meeting, RoleRepository roleRepo, PermissionContext? perms = null) {

        // Draft meetings are never publicly visible.
        if (meeting.Status == MeetingStatus.Draft) {
            if (userId == null)
                return false;
            if (IsDirectOfficerOrDelegate(userId, meeting.ChapterId, roleRepo))
                return true;
            // Users with any meeting permission on the chapter can also see drafts
            // (they need to view what they create/edit). Skip when perms is null —
            // callers without DI access (notably the role-only unit tests) only
            // exercise the direct-officer branch.
            if (perms != null && HasAnyMeetingPermission(meeting.ChapterId, perms))
                return true;
            return false;
        }

        if (meeting.Visibility == MeetingVisibility.Public)
            return true;

        // Private non-draft — requires direct officer OR delegate role on the exact chapter.
        return IsDirectOfficerOrDelegate(userId, meeting.ChapterId, roleRepo);
    }

    private static bool IsDirectOfficerOrDelegate(Guid? userId, Guid chapterId, RoleRepository roleRepo) {
        if (userId == null)
            return false;
        return roleRepo.HasDirectRoleAssignment(
            userId.Value,
            chapterId,
            PermissionIdentifier.SystemRole.ChapterOfficer,
            PermissionIdentifier.SystemRole.GeneralChapterDelegate);
    }

    private static bool HasAnyMeetingPermission(Guid chapterId, PermissionContext perms) {
        foreach (var perm in new[] { PermissionIdentifier.ViewMeetings, PermissionIdentifier.CreateMeetings, PermissionIdentifier.EditMeetings }) {
            if (perms.Has(chapterId, perm))
                return true;
        }
        return false;
    }
}
