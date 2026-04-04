using System;
using LinqToDB.Mapping;
using Quartermaster.Api.Events;

namespace Quartermaster.Data.Events;

[Table(TableName, IsColumnAttributeRequired = false)]
public class Event {
    public const string TableName = "Events";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChapterId { get; set; }
    public string InternalName { get; set; } = "";
    public string PublicName { get; set; } = "";
    public string? Description { get; set; }
    public DateTime? EventDate { get; set; }
    public EventStatus Status { get; set; } = EventStatus.Draft;
    public EventVisibility Visibility { get; set; } = EventVisibility.Private;
    public Guid? EventTemplateId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
