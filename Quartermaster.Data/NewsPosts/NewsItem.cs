using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.NewsPosts;

[Table(TableName, IsColumnAttributeRequired = false)]
public class NewsItem {
    public const string TableName = "NewsItems";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FeedId { get; set; }
    public string ExternalId { get; set; } = "";
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public string Teaser { get; set; } = "";
    public string? ArticleText { get; set; }
    public bool HasFullText { get; set; }
    public DateTime PublishedAt { get; set; }
    public DateTime FetchedAt { get; set; }
    public NewsItemStatus Status { get; set; }
    public string? TriageReason { get; set; }
    public double? BestSimilarity { get; set; }

    /// <summary>JSON array of <see cref="ProgrammeSection"/> ids, best match first.</summary>
    public string MatchedSectionIds { get; set; } = "[]";
    public int FailureCount { get; set; }
    public string? LastError { get; set; }
    public DateTime UpdatedAt { get; set; }
}
