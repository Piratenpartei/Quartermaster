namespace Quartermaster.Server.Notifications;

/// <summary>One catalog entry describing a notification trigger for the preferences UI.</summary>
/// <param name="RequiredGlobalPermission">When set, the trigger is listed and accepted only for callers holding this global permission.</param>
public record NotificationTriggerDescriptor(
    string TriggerId,
    string DisplayName,
    string Description,
    string? RequiredGlobalPermission = null);
