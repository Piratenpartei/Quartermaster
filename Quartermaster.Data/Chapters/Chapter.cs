using System;
using LinqToDB.Mapping;
using Quartermaster.Api.Chapters;

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
    public DateTime? DeletedAt { get; set; }

    public ChapterDTO ToDto() => new() {
        Id = Id,
        Name = Name,
        ShortCode = ShortCode,
        ExternalCode = ExternalCode,
        ParentChapterId = ParentChapterId,
        AdministrativeDivisionId = AdministrativeDivisionId
    };
}
