using System;
using System.Collections.Generic;

namespace Quartermaster.Api.Notifications;

public enum NotificationStatsGroupByDTO {
    None = 0,
    Trigger = 1,
    Template = 2,
    SourceEntityType = 3,
    Channel = 4
}

public class NotificationStatsBucketDTO {
    public string Key { get; set; } = "";
    public int Sent { get; set; }
    public int MdnRequested { get; set; }
    public int MdnReceived { get; set; }
    public int Bounced { get; set; }
}

public class NotificationStatsDTO {
    public int Sent { get; set; }
    public int Failed { get; set; }
    public int MdnRequested { get; set; }
    public int MdnReceived { get; set; }
    public int Bounced { get; set; }
    public double AverageTimeToReadSeconds { get; set; }
    public List<NotificationStatsBucketDTO> Buckets { get; set; } = new();
}
