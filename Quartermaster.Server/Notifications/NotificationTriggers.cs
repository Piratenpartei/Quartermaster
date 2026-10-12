namespace Quartermaster.Server.Notifications;

/// <summary>
/// Stable identifiers for every event that may fire a notification. Used to look up
/// recipient resolvers, template option keys, and audit-log rows.
/// </summary>
public static class NotificationTriggers {
    public const string MotionSubmitted = "motion_submitted";
    public const string ApplicationSubmitted = "application_submitted";
    public const string DueSelectionSubmitted = "due_selection_submitted";
    public const string AssemblyMotionCoSubmitterReset = "assembly_motion_cosubmitter_reset";
    public const string AssemblyMotionCollectingReminder = "assembly_motion_collecting_reminder";
    public const string AssemblyMotionSubmitted = "assembly_motion_submitted";
    public const string AssemblyMotionLapsed = "assembly_motion_lapsed";
    public const string AssemblyMotionChecked = "assembly_motion_checked";
    public const string AssemblyMotionInquiryAsked = "assembly_motion_inquiry_asked";
    public const string AssemblyMotionInquiryAnswered = "assembly_motion_inquiry_answered";
    public const string AssemblyMotionEditedByCommission = "assembly_motion_edited_by_commission";
    public const string AssemblyMotionCompetitionDeclared = "assembly_motion_competition_declared";
    public const string AssemblyMotionWithdrawn = "assembly_motion_withdrawn";
}
