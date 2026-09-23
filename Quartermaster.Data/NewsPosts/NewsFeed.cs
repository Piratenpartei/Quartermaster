using System;
using LinqToDB.Mapping;
using Quartermaster.Api.NewsPosts;

namespace Quartermaster.Data.NewsPosts;

[Table(TableName, IsColumnAttributeRequired = false)]
public class NewsFeed {
    public const string TableName = "NewsFeeds";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ChapterId { get; set; }
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public NewsFeedKind Kind { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime? LastPolledAt { get; set; }
    public string? LastError { get; set; }

    public NewsFeedDTO ToDto() => new() {
        Id = Id,
        Name = Name,
        Url = Url,
        Kind = (int)Kind,
        IsEnabled = IsEnabled,
        LastPolledAt = LastPolledAt == null ? null : new DateTimeOffset(DateTime.SpecifyKind(LastPolledAt.Value, DateTimeKind.Utc)),
        LastError = LastError
    };
}
