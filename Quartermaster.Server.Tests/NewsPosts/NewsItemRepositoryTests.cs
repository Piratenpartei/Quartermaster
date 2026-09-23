using LinqToDB;
using Quartermaster.Data.NewsPosts;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.NewsPosts;

public class NewsItemRepositoryTests : RepositoryTestBase {
    private NewsFeedRepository _feedRepo = default!;
    private NewsItemRepository _itemRepo = default!;
    private PostPublicationRepository _publicationRepo = default!;
    private NewsFeed _feed = default!;

    [Before(Test)]
    public void Setup() {
        _feedRepo = new NewsFeedRepository(Db);
        _itemRepo = new NewsItemRepository(Db);
        _publicationRepo = new PostPublicationRepository(Db);
        _feed = new NewsFeed { Name = "Test", Url = "https://example.org/feed", Kind = NewsFeedKind.Rss, IsEnabled = true };
        _feedRepo.Create(_feed);
    }

    private NewsItem NewItem(string externalId, NewsItemStatus status = NewsItemStatus.New, DateTime? updatedAt = null) {
        var now = updatedAt ?? DateTime.UtcNow;
        return new NewsItem {
            FeedId = _feed.Id,
            ExternalId = externalId,
            Url = "https://example.org/" + externalId,
            Title = "Titel " + externalId,
            Teaser = "Teaser",
            PublishedAt = now,
            FetchedAt = now,
            UpdatedAt = now,
            Status = status
        };
    }

    [Test]
    public async Task TryInsert_ignores_duplicates_per_feed() {
        await Assert.That(_itemRepo.TryInsert(NewItem("a"))).IsTrue();
        await Assert.That(_itemRepo.TryInsert(NewItem("a"))).IsFalse();
        await Assert.That(Db.NewsItems.Count()).IsEqualTo(1);
    }

    [Test]
    public async Task TryClaimForPosting_succeeds_once() {
        var item = NewItem("b", NewsItemStatus.Generated);
        _itemRepo.TryInsert(item);

        await Assert.That(_itemRepo.TryClaimForPosting(item.Id)).IsTrue();
        await Assert.That(_itemRepo.TryClaimForPosting(item.Id)).IsFalse();
        await Assert.That(_itemRepo.Get(item.Id)!.Status).IsEqualTo(NewsItemStatus.Posting);
    }

    [Test]
    public async Task ReplaceCandidates_removes_previous_set() {
        var item = NewItem("c", NewsItemStatus.Relevant);
        _itemRepo.TryInsert(item);
        var sectionId = Guid.NewGuid();

        _itemRepo.ReplaceCandidates(item.Id, [new PostCandidate { Text = "eins", SectionId = sectionId }]);
        _itemRepo.ReplaceCandidates(item.Id, [new PostCandidate { Text = "zwei", SectionId = sectionId }, new PostCandidate { Text = "drei", SectionId = sectionId }]);

        var candidates = _itemRepo.GetCandidates(item.Id);
        await Assert.That(candidates.Select(c => c.Text).ToList()).IsEquivalentTo(new List<string> { "zwei", "drei" });
        await Assert.That(candidates.Select(c => c.SortOrder).ToList()).IsEquivalentTo(new List<int> { 0, 1 });
    }

    [Test]
    public async Task UpdateIfStatus_writes_when_status_still_matches() {
        var item = NewItem("f", NewsItemStatus.Relevant);
        _itemRepo.TryInsert(item);
        item.Status = NewsItemStatus.Generated;
        item.LastError = "x";

        var updated = _itemRepo.UpdateIfStatus(item, NewsItemStatus.Relevant);

        await Assert.That(updated).IsTrue();
        var stored = _itemRepo.Get(item.Id)!;
        await Assert.That(stored.Status).IsEqualTo(NewsItemStatus.Generated);
        await Assert.That(stored.LastError).IsEqualTo("x");
    }

    [Test]
    public async Task UpdateIfStatus_does_nothing_when_status_moved_on() {
        var item = NewItem("g", NewsItemStatus.Relevant);
        _itemRepo.TryInsert(item);
        _itemRepo.SetStatus(item.Id, NewsItemStatus.Posted);
        item.Status = NewsItemStatus.Generated;

        var updated = _itemRepo.UpdateIfStatus(item, NewsItemStatus.Relevant);

        await Assert.That(updated).IsFalse();
        await Assert.That(_itemRepo.Get(item.Id)!.Status).IsEqualTo(NewsItemStatus.Posted);
    }

    [Test]
    public async Task DeleteStale_keeps_items_with_publications() {
        var old = DateTime.UtcNow.AddDays(-40);
        var stale = NewItem("d", NewsItemStatus.Irrelevant, old);
        var posted = NewItem("e", NewsItemStatus.Posted, old);
        _itemRepo.TryInsert(stale);
        _itemRepo.TryInsert(posted);
        _publicationRepo.Create(new PostPublication {
            NewsItemId = posted.Id,
            FinalText = "x",
            ChannelIds = "[]",
            PostizPostIds = "[]",
            Status = PostPublicationStatus.Sent,
            ApprovedByUserId = Guid.NewGuid(),
            ApprovedAt = old
        });

        var deleted = _itemRepo.DeleteStale(DateTime.UtcNow.AddDays(-30));

        await Assert.That(deleted).IsEqualTo(1);
        await Assert.That(_itemRepo.Get(stale.Id)).IsNull();
        await Assert.That(_itemRepo.Get(posted.Id)).IsNotNull();
    }
}
