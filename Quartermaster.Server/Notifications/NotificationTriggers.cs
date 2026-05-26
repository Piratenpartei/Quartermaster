namespace Quartermaster.Server.Notifications;

/// <summary>
/// Stable identifiers for every event that may fire a notification. Used to look up
/// recipient resolvers, template option keys, and audit-log rows.
/// </summary>
public static class NotificationTriggers {
    public const string MotionSubmitted = "motion_submitted";
}
