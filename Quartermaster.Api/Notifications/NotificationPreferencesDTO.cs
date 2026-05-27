using System.Collections.Generic;

namespace Quartermaster.Api.Notifications;

/// <summary>Full preferences view for the calling user: one row per (trigger × channel).</summary>
public class NotificationPreferencesDTO {
    public List<NotificationTriggerDescriptorDTO> Triggers { get; set; } = new();
    public List<NotificationChannelDescriptorDTO> Channels { get; set; } = new();
    public List<NotificationPreferenceCellDTO> Cells { get; set; } = new();
}

public class NotificationTriggerDescriptorDTO {
    public string TriggerId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
}

public class NotificationChannelDescriptorDTO {
    public string ChannelId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    /// <summary>False when the channel exists but isn't yet wired (Phase 4 / PDF rendering).</summary>
    public bool Available { get; set; }
}

public class NotificationPreferenceCellDTO {
    public string TriggerId { get; set; } = "";
    public string ChannelId { get; set; } = "";
    public bool Enabled { get; set; }
}

public class UpdateNotificationPreferencesRequest {
    public List<NotificationPreferenceCellDTO> Cells { get; set; } = new();
}
