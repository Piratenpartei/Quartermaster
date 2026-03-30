using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.Chapters;

[Table(TableName, IsColumnAttributeRequired = false)]
public class Chapter {
    public const string TableName = "Chapters";

    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public Guid? AdministrativeDivisionId { get; set; }
    public Guid? ParentChapterId { get; set; }
    public string? ShortCode { get; set; }
    public string? ExternalCode { get; set; }
}
