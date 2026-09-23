using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.NewsPosts;

[Table(TableName, IsColumnAttributeRequired = false)]
public class PostCandidate {
    public const string TableName = "PostCandidates";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NewsItemId { get; set; }
    public string Text { get; set; } = "";
    public Guid SectionId { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}
