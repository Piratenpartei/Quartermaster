using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.NewsPosts;

[Table(TableName, IsColumnAttributeRequired = false)]
public class ProgrammeSource {
    public const string TableName = "ProgrammeSources";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ChapterId { get; set; }

    /// <summary>A wiki page title, or a prefix ending in "/" that stands for every subpage (e.g. <c>Positionspapiere/</c>).</summary>
    public string WikiPageTitle { get; set; } = "";
    public DateTime? LastSyncedAt { get; set; }
    public long? LastRevisionId { get; set; }
    public string? LastError { get; set; }

    [NotColumn]
    public bool IsPrefix => WikiPageTitle.EndsWith('/');
}
