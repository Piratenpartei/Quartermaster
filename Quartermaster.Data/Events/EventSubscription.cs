using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.Events;

/// <summary>A user's hand-made subscription to an event's reminders and submission notices.</summary>
[Table(TableName, IsColumnAttributeRequired = false)]
public class EventSubscription {
    public const string TableName = "EventSubscriptions";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EventId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
