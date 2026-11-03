using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.Events;

/// <summary>A reminder that went out for one subscription; <see cref="TargetAt"/> is the deadline or start it was about, so a moved time is due again.</summary>
[Table(TableName, IsColumnAttributeRequired = false)]
public class EventReminderDelivery {
    public const string TableName = "EventReminderDeliveries";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SubscriptionId { get; set; }
    public EventReminderKind Kind { get; set; }
    public DateTime TargetAt { get; set; }
    public DateTime SentAt { get; set; }
}
