using System;

namespace Quartermaster.Server.NewsPosts.Feeds;

public record ParsedFeedItem(string ExternalId, string Url, string Title, string Teaser, string? ArticleText, DateTime PublishedAt, string? DetailsUrl);
