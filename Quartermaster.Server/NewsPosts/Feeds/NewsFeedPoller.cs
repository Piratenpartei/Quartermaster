using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartermaster.Data.NewsPosts;

namespace Quartermaster.Server.NewsPosts.Feeds;

public class NewsFeedPoller {
    private readonly NewsFeedRepository _feedRepo;
    private readonly NewsItemRepository _itemRepo;
    private readonly IFeedFetcher _fetcher;
    private readonly ILogger<NewsFeedPoller> _logger;

    public NewsFeedPoller(NewsFeedRepository feedRepo, NewsItemRepository itemRepo, IFeedFetcher fetcher, ILogger<NewsFeedPoller> logger) {
        _feedRepo = feedRepo;
        _itemRepo = itemRepo;
        _fetcher = fetcher;
        _logger = logger;
    }

    public async Task<int> PollAllAsync(TimeSpan maxItemAge, DateTime nowUtc, CancellationToken ct) {
        var inserted = 0;
        foreach (var feed in _feedRepo.GetEnabled()) {
            try {
                inserted += await PollFeedAsync(feed, maxItemAge, nowUtc, ct);
                _feedRepo.MarkPolled(feed.Id, nowUtc, null);
            } catch (OperationCanceledException) when (ct.IsCancellationRequested) {
                throw;
            } catch (Exception ex) {
                _logger.LogWarning(ex, "Polling feed {Feed} failed", feed.Name);
                _feedRepo.MarkPolled(feed.Id, nowUtc, ex.Message);
            }
        }
        return inserted;
    }

    private async Task<int> PollFeedAsync(NewsFeed feed, TimeSpan maxItemAge, DateTime nowUtc, CancellationToken ct) {
        var body = await _fetcher.GetStringAsync(feed.Url, ct);
        var items = feed.Kind == NewsFeedKind.TagesschauApi
            ? TagesschauParser.ParseList(body, nowUtc)
            : RssFeedParser.Parse(body, nowUtc);

        var inserted = 0;
        foreach (var parsed in items) {
            if (nowUtc - parsed.PublishedAt > maxItemAge)
                continue;
            var externalId = Truncate(parsed.ExternalId, 512);
            if (_itemRepo.Exists(feed.Id, externalId))
                continue;

            var articleText = parsed.ArticleText ?? await TryLoadDetailsAsync(parsed, ct);
            var item = new NewsItem {
                FeedId = feed.Id,
                ExternalId = externalId,
                Url = Truncate(parsed.Url, 1024),
                Title = Truncate(parsed.Title, 1024),
                Teaser = parsed.Teaser,
                ArticleText = articleText,
                HasFullText = !string.IsNullOrWhiteSpace(articleText),
                PublishedAt = parsed.PublishedAt,
                FetchedAt = nowUtc,
                UpdatedAt = nowUtc,
                Status = NewsItemStatus.New
            };
            if (_itemRepo.TryInsert(item))
                inserted++;
        }
        return inserted;
    }

    private async Task<string?> TryLoadDetailsAsync(ParsedFeedItem parsed, CancellationToken ct) {
        if (parsed.DetailsUrl == null)
            return null;
        try {
            var text = TagesschauParser.ParseDetails(await _fetcher.GetStringAsync(parsed.DetailsUrl, ct));
            return text.Length == 0 ? null : text;
        } catch (OperationCanceledException) when (ct.IsCancellationRequested) {
            throw;
        } catch (Exception ex) {
            _logger.LogInformation(ex, "Details for {Url} unavailable, using teaser", parsed.Url);
            return null;
        }
    }

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];
}
