using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.Events;

[Table(TableName, IsColumnAttributeRequired = false)]
public class EventTemplate {
    public const string TableName = "EventTemplates";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string PublicNameTemplate { get; set; } = "";
    public string? DescriptionTemplate { get; set; }
    public string Variables { get; set; } = "[]";
    public string ChecklistItemTemplates { get; set; } = "[]";
    public Guid? ChapterId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
