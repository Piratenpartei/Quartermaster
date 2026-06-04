using System.Collections.Generic;

namespace Quartermaster.Api.Notifications;

public class NotificationStatsFilterOptionDTO {
    public string Value { get; set; } = "";
    public string DisplayName { get; set; } = "";
}

public class NotificationStatsCatalogDTO {
    public List<NotificationStatsFilterOptionDTO> Channels { get; set; } = new();
    public List<NotificationStatsFilterOptionDTO> Triggers { get; set; } = new();
    public List<NotificationStatsFilterOptionDTO> Templates { get; set; } = new();
    public List<NotificationStatsFilterOptionDTO> SourceEntityTypes { get; set; } = new();
}
