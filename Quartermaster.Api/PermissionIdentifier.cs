using System.Collections.Generic;

namespace Quartermaster.Api;

public static class PermissionIdentifier {
    // Global permissions
    public static readonly string CreateUser = "users_create";
    public static readonly string ViewUsers = "users_view";
    public static readonly string DeleteUsers = "users_delete";

    public static readonly string CreateChapter = "chapters_create";
    public static readonly string EditChapter = "chapters_edit";
    public static readonly string DeleteChapter = "chapters_delete";
    public static readonly string ArchiveChapter = "chapters_archive";

    public static readonly string ViewOptions = "options_view";
    public static readonly string EditOptions = "options_edit";
    public static readonly string ViewOptionSecrets = "options_view_secrets";

    public static readonly string ViewAudit = "audit_view";
    public static readonly string ViewNotificationLogs = "notificationlogs_view";
    public static readonly string TriggerMemberImport = "member_import_trigger";
    public static readonly string ViewAllMembers = "members_view_all";

    public static readonly string ManageRoles = "roles_manage";

    /// <summary>Lets the holder edit the PDF form templates (layout + Fluid markup) under Administration.</summary>
    public static readonly string EditPdfTemplates = "pdf_templates_edit";

    /// <summary>Lets the holder see the news-post queue, generate candidates, and greenlight posts to Postiz.</summary>
    public static readonly string ReviewNewsPosts = "newsposts_review";

    /// <summary>Lets the holder manage news feeds and programme sources and trigger the programme sync.</summary>
    public static readonly string ManageNewsPosts = "newsposts_manage";

    /// <summary>Lets the holder grant any chapter-scoped permission as a global grant on any user (the standard <see cref="CreateUser"/> + global-perm flow only allows globally-flagged permissions to be granted globally). Intended for root/super-admin accounts that need access to chapter-only flows without joining every chapter as an officer.</summary>
    public static readonly string AssignPermissionsGlobally = "permissions_assign_globally";

    // Global: assign an administrative division/chapter to applications that arrived without one
    // (manual or foreign address) so they can enter the normal review flow.
    public static readonly string LinkApplicationDivision = "applications_link_division";

    // System-wide override permissions (global, prefixed with system_)
    // These grant the permission across ALL chapters regardless of chapter-scoped grants.
    // Applied to the root admin by default.
    public static readonly string SystemVote = "system_vote";

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

    public static readonly string ViewMeetings = "meetings_view";
    public static readonly string CreateMeetings = "meetings_create";
    public static readonly string EditMeetings = "meetings_edit";
    public static readonly string DeleteMeetings = "meetings_delete";

    public static readonly string ManageChapterMail = "chaptermail_manage";

    // Permission groups for nav visibility
    public static readonly List<string> BoardWorkPermissions = [
        ViewMotions, ViewApplications, ViewDueSelections,
        ViewMembers, ViewEvents, ViewTemplates, ViewMeetings
    ];

    public static readonly List<string> SystemPermissions = [
        ViewOptions, ViewUsers, ViewOfficers, ViewAllMembers, TriggerMemberImport, ManageRoles, ViewNotificationLogs, EditPdfTemplates,
        ReviewNewsPosts, ManageNewsPosts
    ];

    // Well-known role identifiers (seeded as system roles)
    public static class SystemRole {
        public const string ChapterOfficer = "chapter_officer";
        public const string GeneralChapterDelegate = "general_chapter_delegate";
    }

    // Default permissions auto-granted to chapter officers
    public static readonly List<string> DefaultOfficerPermissions = [
        ViewMotions, EditMotions, VoteMotions,
        ViewApplications, ProcessApplications,
        ViewDueSelections, ProcessDueSelections,
        ViewEvents, CreateEvents, EditEvents, DeleteEvents,
        ViewMembers,
        ViewOfficers,
        ViewTemplates, EditTemplates,
        ViewMeetings, CreateMeetings, EditMeetings,
        ManageChapterMail
    ];
}