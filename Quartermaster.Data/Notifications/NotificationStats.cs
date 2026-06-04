using System;
using System.Collections.Generic;

namespace Quartermaster.Data.Notifications;

public class NotificationStatsFilter {
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? ChannelId { get; set; }
    public string? TriggerId { get; set; }
    public string? TemplateIdentifier { get; set; }
    public string? SourceEntityType { get; set; }
    public NotificationStatsGroupBy GroupBy { get; set; } = NotificationStatsGroupBy.None;
}

public enum NotificationStatsGroupBy {
    None = 0,
    Trigger = 1,
    Template = 2,
    SourceEntityType = 3,
    Channel = 4
}

public class NotificationStatsAggregate {
    public int Sent { get; set; }
    public int Failed { get; set; }
    public int MdnRequested { get; set; }
    public int MdnReceived { get; set; }
    public int Bounced { get; set; }
    public double AverageTimeToReadSeconds { get; set; }
    public List<NotificationStatsBucket> Buckets { get; set; } = new();
}

public class NotificationStatsBucket {
    public string Key { get; set; } = "";
    public int Sent { get; set; }
    public int MdnRequested { get; set; }
    public int MdnReceived { get; set; }
    public int Bounced { get; set; }
}
