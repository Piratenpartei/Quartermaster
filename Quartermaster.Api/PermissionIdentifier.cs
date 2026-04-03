using System.Collections.Generic;

namespace Quartermaster.Api;

public static class PermissionIdentifier {
    // Global permissions
    public static readonly string CreateUser = "users_create";
    public static readonly string ViewUsers = "users_view";

    public static readonly string CreateChapter = "chapters_create";

    public static readonly string ViewOptions = "options_view";
    public static readonly string EditOptions = "options_edit";

    public static readonly string ViewAudit = "audit_view";
    public static readonly string ViewEmailLogs = "emaillogs_view";
    public static readonly string TriggerMemberImport = "member_import_trigger";

    // Chapter-scoped permissions
    public static readonly string ViewApplications = "applications_view";
    public static readonly string ProcessApplications = "applications_process";
    public static readonly string ViewDueSelections = "dueselections_view";
    public static readonly string ProcessDueSelections = "dueselections_process";

    public static readonly string ViewEvents = "events_view";
    public static readonly string CreateEvents = "events_create";
    public static readonly string EditEvents = "events_edit";
    public static readonly string DeleteEvents = "events_delete";

    public static readonly string ViewMotions = "motions_view";
    public static readonly string EditMotions = "motions_edit";
    public static readonly string VoteMotions = "motions_vote";
    public static readonly string VoteDelegateMotions = "motions_vote_delegate";

    public static readonly string ViewMembers = "members_view";
    public static readonly string EditMembers = "members_edit";

    public static readonly string ViewOfficers = "officers_view";
    public static readonly string EditOfficers = "officers_edit";

    public static readonly string ViewTemplates = "templates_view";
    public static readonly string EditTemplates = "templates_edit";

    // Permission groups for nav visibility
    public static readonly List<string> BoardWorkPermissions = [
        ViewMotions, ViewApplications, ViewDueSelections,
        ViewMembers, ViewEvents, ViewTemplates
    ];

    public static readonly List<string> SystemPermissions = [
        ViewOptions, ViewUsers, ViewOfficers
    ];
}