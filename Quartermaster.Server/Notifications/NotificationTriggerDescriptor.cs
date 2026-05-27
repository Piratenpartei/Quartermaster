namespace Quartermaster.Server.Notifications;

/// <summary>One catalog entry describing a notification trigger for the preferences UI.</summary>
public record NotificationTriggerDescriptor(string TriggerId, string DisplayName, string Description);
