using System.Collections.Generic;

namespace Quartermaster.Server.Notifications;

/// <summary>
/// Display catalog for the notification-preferences UI. Each new trigger added to
/// <see cref="NotificationTriggers"/> should also get an entry here with a German
/// user-facing label and one-line description.
/// </summary>
public static class NotificationTriggerCatalog {
    public static IReadOnlyList<NotificationTriggerDescriptor> All { get; } = new[] {
        new NotificationTriggerDescriptor(
            NotificationTriggers.MotionSubmitted,
            "Neuer Antrag",
            "Sobald ein neuer Antrag in einer von dir betreuten Gliederung eingereicht wird."),
        new NotificationTriggerDescriptor(
            NotificationTriggers.ApplicationSubmitted,
            "Neuer Mitgliedsantrag",
            "Sobald ein Mitgliedsantrag für eine von dir betreuten Gliederung eingeht."),
        new NotificationTriggerDescriptor(
            NotificationTriggers.DueSelectionSubmitted,
            "Neue Beitragseinstufung",
            "Sobald eine Beitragsminderung für ein Mitglied einer von dir betreuten Gliederung eingeht.")
    };
}
