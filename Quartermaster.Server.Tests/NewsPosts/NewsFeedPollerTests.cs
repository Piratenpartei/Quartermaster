using Microsoft.Extensions.Logging.Abstractions;
using Quartermaster.Data.NewsPosts;
using Quartermaster.Server.NewsPosts.Feeds;
using Quartermaster.Server.Tests.Infrastructure;
using Quartermaster.Server.Tests.NewsPosts.Fakes;

namespace Quartermaster.Server.Tests.NewsPosts;

public class NewsFeedPollerTests : RepositoryTestBase {
    private static readonly DateTime Now = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);

    private NewsFeedRepository _feedRepo = default!;
    private NewsItemRepository _itemRepo = default!;
    private FakeFeedFetcher _fetcher = default!;
    private NewsFeedPoller _poller = default!;

    [Before(Test)]
    public void Setup() {
        _feedRepo = new NewsFeedRepository(Db);
        _itemRepo = new NewsItemRepository(Db);
        _fetcher = new FakeFeedFetcher();
        _poller = new NewsFeedPoller(_feedRepo, _itemRepo, _fetcher, NullLogger<NewsFeedPoller>.Instance);
    }

    [Test]
    public async Task Polling_twice_inserts_each_item_once() {
        _feedRepo.Create(new NewsFeed { Name = "np", Url = "https://np/feed", Kind = NewsFeedKind.Rss, IsEnabled = true });
        _fetcher.Responses["https://np/feed"] = FakeFeedFetcher.Load("netzpolitik.xml");

        var first = await _poller.PollAllAsync(TimeSpan.FromHours(48), Now, CancellationToken.None);
        var second = await _poller.PollAllAsync(TimeSpan.FromHours(48), Now, CancellationToken.None);

        await Assert.That(first).IsEqualTo(2);
        await Assert.That(second).IsEqualTo(0);
        var stored = _itemRepo.GetByStatus(NewsItemStatus.New);
        await Assert.That(stored.Single(i => i.ExternalId == "https://netzpolitik.org/?p=1").HasFullText).IsTrue();
    }

    [Test]
    public async Task Items_older_than_max_age_are_ignored() {
        _feedRepo.Create(new NewsFeed { Name = "np", Url = "https://np/feed", Kind = NewsFeedKind.Rss, IsEnabled = true });
        _fetcher.Responses["https://np/feed"] = FakeFeedFetcher.Load("netzpolitik.xml");

        var inserted = await _poller.PollAllAsync(TimeSpan.FromHours(1), Now, CancellationToken.None);

        await Assert.That(inserted).IsEqualTo(1);
    }

    [Test]
    public async Task Tagesschau_loads_details_and_a_failing_detail_falls_back_to_teaser() {
        _feedRepo.Create(new NewsFeed { Name = "ts", Url = "https://ts/news", Kind = NewsFeedKind.TagesschauApi, IsEnabled = true });
        _fetcher.Responses["https://ts/news"] = FakeFeedFetcher.Load("tagesschau-news.json");
        _fetcher.Responses["https://www.tagesschau.de/api2u/inland/tickets-100.json"] = FakeFeedFetcher.Load("tagesschau-details.json");

        await _poller.PollAllAsync(TimeSpan.FromHours(48), Now, CancellationToken.None);

        var items = _itemRepo.GetByStatus(NewsItemStatus.New);
        await Assert.That(items.Single(i => i.ExternalId == "ts-1").HasFullText).IsTrue();
        await Assert.That(items.Single(i => i.ExternalId == "ts-2").HasFullText).IsFalse();
    }

    [Test]
    public async Task Cancellation_during_details_fetch_propagates_and_leaves_feed_unpolled() {
        var feed = new NewsFeed { Name = "ts", Url = "https://ts/news", Kind = NewsFeedKind.TagesschauApi, IsEnabled = true };
        _feedRepo.Create(feed);
        _fetcher.Responses["https://ts/news"] = FakeFeedFetcher.Load("tagesschau-news.json");
        _fetcher.Responses["https://www.tagesschau.de/api2u/inland/tickets-100.json"] = FakeFeedFetcher.Load("tagesschau-details.json");
        _fetcher.CancelledUrls.Add("https://www.tagesschau.de/api2u/inland/ohne-100.json");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => _poller.PollAllAsync(TimeSpan.FromHours(48), Now, cts.Token));

        var items = _itemRepo.GetByStatus(NewsItemStatus.New);
        await Assert.That(items.Any(i => i.ExternalId == "ts-1")).IsTrue();
        await Assert.That(items.Any(i => i.ExternalId == "ts-2")).IsFalse();
        await Assert.That(_feedRepo.Get(feed.Id)!.LastPolledAt).IsNull();
    }

    [Test]
    public async Task Failing_feed_records_error_and_others_still_poll() {
        var broken = new NewsFeed { Name = "broken", Url = "https://broken/feed", Kind = NewsFeedKind.Rss, IsEnabled = true };
        _feedRepo.Create(broken);
        _feedRepo.Create(new NewsFeed { Name = "np", Url = "https://np/feed", Kind = NewsFeedKind.Rss, IsEnabled = true });
        _fetcher.Responses["https://np/feed"] = FakeFeedFetcher.Load("netzpolitik.xml");

        var inserted = await _poller.PollAllAsync(TimeSpan.FromHours(48), Now, CancellationToken.None);

        await Assert.That(inserted).IsEqualTo(2);
        await Assert.That(_feedRepo.Get(broken.Id)!.LastError).Contains("404");
    }
}
