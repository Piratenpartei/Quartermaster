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
            "Sobald eine Beitragsminderung für ein Mitglied einer von dir betreuten Gliederung eingeht."),
        new NotificationTriggerDescriptor(
            NotificationTriggers.AssemblyMotionCoSubmitterReset,
            "Antrag geändert, Mitantragstellung zurückgesetzt",
            "Sobald die antragstellende Person einen Antrag ändert, bei dem du Mitantragsteller:in warst."),
        new NotificationTriggerDescriptor(
            NotificationTriggers.AssemblyMotionCollectingReminder,
            "Erinnerung: Mitantragstellende gesucht",
            "48 Stunden vor Ablauf der Frist, wenn dein Antrag noch nicht genug Mitantragstellende hat."),
        new NotificationTriggerDescriptor(
            NotificationTriggers.AssemblyMotionSubmitted,
            "Antrag eingereicht (Antragskommission)",
            "Sobald ein Antrag zu einer Versammlung deiner betreuten Gliederung eingereicht wird."),
        new NotificationTriggerDescriptor(
            NotificationTriggers.AssemblyMotionLapsed,
            "Antrag verfallen",
            "Sobald dein Antrag verfällt, weil die Frist ohne genug Mitantragstellende abgelaufen ist."),
        new NotificationTriggerDescriptor(
            NotificationTriggers.AssemblyMotionChecked,
            "Antrag geprüft",
            "Sobald die Antragskommission die formale Prüfung deines Antrags abgeschlossen hat."),
        new NotificationTriggerDescriptor(
            NotificationTriggers.AssemblyMotionInquiryAsked,
            "Rückfrage zu deinem Antrag",
            "Sobald die Antragskommission eine Rückfrage zu deinem Antrag stellt."),
        new NotificationTriggerDescriptor(
            NotificationTriggers.AssemblyMotionInquiryAnswered,
            "Rückfrage beantwortet (Antragskommission)",
            "Sobald eine Rückfrage der Antragskommission beantwortet wird."),
        new NotificationTriggerDescriptor(
            NotificationTriggers.AssemblyMotionEditedByCommission,
            "Antrag von der Antragskommission bearbeitet",
            "Sobald die Antragskommission den Text deines Antrags ändert."),
        new NotificationTriggerDescriptor(
            NotificationTriggers.AssemblyMotionCompetitionDeclared,
            "Konkurrenzantrag erklärt",
            "Sobald ein anderer Antrag als Konkurrenz zu deinem Antrag erklärt wird."),
        new NotificationTriggerDescriptor(
            NotificationTriggers.AssemblyMotionWithdrawn,
            "Antrag zurückgezogen",
            "Sobald ein Antrag zurückgezogen wird, an dem du beteiligt bist oder den du als Antragskommission betreust.")
    };
}
