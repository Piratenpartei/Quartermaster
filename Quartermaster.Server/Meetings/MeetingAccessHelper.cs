using System;
using Quartermaster.Api;
using Quartermaster.Api.Meetings;
using Quartermaster.Data.Meetings;
using Quartermaster.Data.Roles;

namespace Quartermaster.Server.Meetings;

/// <summary>
/// Encapsulates the view-access rule for meetings:
/// - Public meetings: visible to everyone (including anonymous).
/// - Private meetings: visible only to users holding a direct ChapterOfficer or
///   GeneralChapterDelegate role assignment on the meeting's chapter (no inheritance).
/// </summary>
public static class MeetingAccessHelper {
    public static bool CanUserViewMeeting(Guid? userId, Meeting meeting, RoleRepository roleRepo) {
        if (meeting.Visibility == MeetingVisibility.Public)
            return true;

        // Private — requires direct officer OR delegate role on the meeting's exact chapter.
        if (userId == null)
            return false;
        return roleRepo.HasDirectRoleAssignment(
            userId.Value,
            meeting.ChapterId,
            PermissionIdentifier.SystemRole.ChapterOfficer,
            PermissionIdentifier.SystemRole.GeneralChapterDelegate);
    }
}
