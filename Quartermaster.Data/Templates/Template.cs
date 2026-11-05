using System;
using LinqToDB.Mapping;
using Quartermaster.Api.Templates;

namespace Quartermaster.Data.Templates;

[Table(TableName, IsColumnAttributeRequired = false)]
public class Template {
    public const string TableName = "Templates";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string? Identifier { get; set; }
    public string DisplayName { get; set; } = "";
    public bool IsSystem { get; set; }
    public Guid? ChapterId { get; set; }
    public Guid? BaseTemplateId { get; set; }
    public string? Subject { get; set; }
    public string Body { get; set; } = "";
    public bool AllowsMemberFields { get; set; }
    public bool AllowsEventFields { get; set; }
    public bool AllowsChapterFields { get; set; }
    public TemplateRenderMode RenderMode { get; set; }

    /// <summary>Hash of the seed text a system template was last written from; null for chapter copies and rows seeded before V9.1.</summary>
    public string? SeedHash { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
