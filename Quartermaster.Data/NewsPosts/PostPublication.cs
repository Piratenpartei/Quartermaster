using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.NewsPosts;

[Table(TableName, IsColumnAttributeRequired = false)]
public class PostPublication {
    public const string TableName = "PostPublications";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NewsItemId { get; set; }
    public Guid? CandidateId { get; set; }
    public string FinalText { get; set; } = "";

    /// <summary>JSON array of Postiz integration ids.</summary>
    public string ChannelIds { get; set; } = "[]";

    /// <summary>JSON array of Postiz post ids returned by the create call.</summary>
    public string PostizPostIds { get; set; } = "[]";
    public DateTime? ScheduledFor { get; set; }
    public PostPublicationStatus Status { get; set; }
    public string? Error { get; set; }
    public Guid ApprovedByUserId { get; set; }
    public DateTime ApprovedAt { get; set; }
}
