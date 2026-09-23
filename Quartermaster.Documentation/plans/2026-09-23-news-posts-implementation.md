# News Posts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Quartermaster polls news feeds, matches items against the Piratenpartei programme, has Mistral write German post candidates, and publishes a human-greenlit candidate through the self-hosted Postiz API.

**Architecture:** Data lives in new LinqToDB entities under `Quartermaster.Data/NewsPosts/` (folded into migration `M003`). Server logic lives in `Quartermaster.Server/NewsPosts/`, split into pure units (wikitext splitter, feed parsers, cosine index, candidate validation, Postiz request builder) and thin HTTP adapters behind interfaces (`IMistralClient`, `IPostizClient`, `IWikiClient`, `IFeedFetcher`) so every service is tested with fakes. Two hosted services drive the programme sync (weekly) and the news pipeline (interval); FastEndpoints expose review and settings APIs; two Blazor pages form the "Social Media" section.

**Tech Stack:** .NET 10, FastEndpoints, LinqToDB + FluentMigrator on MariaDB, Blazor WASM, TUnit, System.Text.Json, System.Xml.Linq. No new NuGet packages.

**Spec:** `Quartermaster.Documentation/plans/2026-09-23-news-posts-design.md`

## Global Constraints

- Follow `CLAUDE.md`: K&R braces, if-body on next line, braces required for multi-line conditions and any else, no `@code` in `.razor` (code-behind `.razor.cs`), explicit access modifiers, `var` everywhere, fluent LINQ only, `== null` / `!= null` never `is null`/`is { }`, never `and`/`or` patterns, no `using` aliases, no `global::`, one class per file (DTO files and request+endpoint pairs excepted), no region comments, comments only for non-obvious why.
- Readonly fields `_camelCase`; mutable private fields plain `camelCase`; fluent chains one call per line.
- Code English; user-facing strings German via i18n keys in both `Quartermaster.Server/wwwroot/i18n/de.json` and `en.json`.
- Enums persisted as ints are append-only.
- All schema goes into the existing `Quartermaster.Data/Migrations/M003.cs` (V3 is not deployed). Every new table is also added to the TRUNCATE list in `Quartermaster.Server.Tests/Infrastructure/WorkerDatabase.cs`.
- Post text limit: 280, counted in text elements (`StringInfo.LengthInTextElements`), on client and server alike.
- Wiki API requests must send the User-Agent `Quartermaster/3 (+https://piratenpartei.de)`; a browser-like UA gets the Anubis HTML page. An HTML body is a fetch error.
- Tests never call Mistral, Postiz, the wiki or any feed over the network.
- Commits are made by the user only. Where a task ends in "Checkpoint", stop and report; do not run git.
- Build: `dotnet build Quartermaster.sln -m:2 -v:q -nologo`. Run one test class: `dotnet run --no-build --project Quartermaster.Server.Tests/Quartermaster.Server.Tests.csproj -- --treenode-filter "/*/*/<ClassName>/*"`. Never run the full suite.

## Review Focus

1. A feed item without a parseable date, without a GUID, or repeated in a later poll: it must get `PublishedAt = now`, dedup on `Url` when there is no GUID, and never be inserted twice. Pinned in Task 6 (`FeedParserTests`, `NewsFeedPollerTests`).
2. Mistral answering with Markdown-fenced JSON or non-JSON text: the item's stage counts a failure, the run continues with the next item, nothing throws out of the pipeline. Pinned in Task 7 (`CandidateGeneratorTests`) and Task 8 (`NewsPipelineTests`).
3. Post text with emoji or combined characters near the 280 limit: client counter, generator validation and publish endpoint must agree. Pinned in Task 1 (`PostTextTests`) and Task 9 (`PublishEndpointTests`).
4. A scheduled time in the past (including a few minutes in the past because the form sat open): rejected with 400, never forwarded to Postiz; a future time with a non-UTC offset reaches Postiz as the correct UTC instant. Pinned in Task 9 (`PublishEndpointTests`).
5. A Postiz channel disabled or removed between page load and greenlight: 400 with a German message, item stays `Generated`, no Postiz call. Pinned in Task 9 (`PublishEndpointTests`).

---

## File Structure

```
Quartermaster.Api/NewsPosts/
  PostText.cs                      Shared 280-text-element rule (client + server)
  NewsPostDTOs.cs                  All request/response DTOs and the DTO-side enums
Quartermaster.Data/NewsPosts/
  NewsFeedKind.cs, NewsItemStatus.cs, PostPublicationStatus.cs
  ProgrammeSource.cs, ProgrammeSection.cs, NewsFeed.cs, NewsItem.cs, PostCandidate.cs, PostPublication.cs
  ProgrammeRepository.cs           Sources + sections
  NewsFeedRepository.cs
  NewsItemRepository.cs            Items + candidates, status transitions, retention
  PostPublicationRepository.cs
  NewsPostsSeeder.cs               Default feeds + programme sources
Quartermaster.Server/NewsPosts/
  NewsPostsSettings.cs             Typed read of all newsposts.* options
  Mistral/IMistralClient.cs, Mistral/HttpMistralClient.cs, Mistral/MistralException.cs
  Postiz/IPostizClient.cs, Postiz/HttpPostizClient.cs, Postiz/PostizChannel.cs, Postiz/PostizRequestBuilder.cs, Postiz/PostizException.cs
  Programme/IWikiClient.cs, Programme/HttpWikiClient.cs, Programme/WikiPage.cs
  Programme/WikiSection.cs, Programme/WikitextSectionSplitter.cs, Programme/WikiMarkupStripper.cs
  Programme/ProgrammeIndex.cs, Programme/SectionMatch.cs, Programme/EmbeddingCodec.cs
  Programme/ProgrammeSyncService.cs, Programme/ProgrammeSyncHostedService.cs
  Feeds/IFeedFetcher.cs, Feeds/HttpFeedFetcher.cs, Feeds/ParsedFeedItem.cs
  Feeds/RssFeedParser.cs, Feeds/TagesschauParser.cs, Feeds/HtmlText.cs, Feeds/NewsFeedPoller.cs
  Generation/RelevanceTriage.cs, Generation/TriageResult.cs, Generation/CandidateGenerator.cs
  Generation/GeneratedCandidate.cs, Generation/CandidateValidator.cs, Generation/LlmJson.cs
  Pipeline/NewsPipeline.cs, Pipeline/NewsPipelineHostedService.cs
  Endpoints/*.cs                   One file per endpoint
Quartermaster.Blazor/Pages/Administration/
  NewsPosts.razor(.cs), NewsPostsSettings.razor(.cs)
Quartermaster.Server.Tests/NewsPosts/
  Fakes/*.cs, *Tests.cs, TestData/NewsPosts/* sample payloads
```

---

### Task 1: Data model, migration, permissions, options, seeding

**Files:**
- Create: `Quartermaster.Api/NewsPosts/PostText.cs`
- Create: `Quartermaster.Data/NewsPosts/NewsFeedKind.cs`, `NewsItemStatus.cs`, `PostPublicationStatus.cs`, `ProgrammeSource.cs`, `ProgrammeSection.cs`, `NewsFeed.cs`, `NewsItem.cs`, `PostCandidate.cs`, `PostPublication.cs`, `ProgrammeRepository.cs`, `NewsFeedRepository.cs`, `NewsItemRepository.cs`, `PostPublicationRepository.cs`, `NewsPostsSeeder.cs`
- Modify: `Quartermaster.Data/Migrations/M003.cs` (append tables in `Up`, drops in `Down`)
- Modify: `Quartermaster.Data/DbContext.cs` (tables, `AddRepositories`, `SupplementDefaults`)
- Modify: `Quartermaster.Api/PermissionIdentifier.cs`, `Quartermaster.Data/Permissions/PermissionRepository.cs`
- Modify: `Quartermaster.Data/Options/OptionRepository.cs`
- Modify: `Quartermaster.Server.Tests/Infrastructure/WorkerDatabase.cs` (TRUNCATE list)
- Modify: `Quartermaster.Documentation/Configuration.md`
- Test: `Quartermaster.Server.Tests/NewsPosts/PostTextTests.cs`, `Quartermaster.Server.Tests/NewsPosts/NewsItemRepositoryTests.cs`

**Interfaces:**
- Produces: all entities and enums below; `PostText.MaxLength`, `PostText.Length(string)`, `PostText.IsValid(string)`; repository methods listed in Step 3; `PermissionIdentifier.ReviewNewsPosts`, `PermissionIdentifier.ManageNewsPosts`; option identifiers `newsposts.*`.

- [ ] **Step 1: Write the failing tests**

`Quartermaster.Server.Tests/NewsPosts/PostTextTests.cs`:

```csharp
using Quartermaster.Api.NewsPosts;

namespace Quartermaster.Server.Tests.NewsPosts;

public class PostTextTests {
    [Test]
    public async Task Counts_emoji_and_combined_characters_as_one() {
        await Assert.That(PostText.Length("a👍🏽é")).IsEqualTo(3);
    }

    [Test]
    public async Task Accepts_exactly_the_limit_and_rejects_one_more() {
        var atLimit = new string('x', 279) + "🏴‍☠️";
        await Assert.That(PostText.IsValid(atLimit)).IsTrue();
        await Assert.That(PostText.IsValid(atLimit + "x")).IsFalse();
    }

    [Test]
    public async Task Rejects_blank_text() {
        await Assert.That(PostText.IsValid("   ")).IsFalse();
    }
}
```

`Quartermaster.Server.Tests/NewsPosts/NewsItemRepositoryTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet build Quartermaster.sln -m:2 -v:q -nologo`
Expected: build errors (`PostText`, `NewsItemRepository` etc. do not exist).

- [ ] **Step 3: Implement**

`Quartermaster.Api/NewsPosts/PostText.cs`:

```csharp
using System.Globalization;

namespace Quartermaster.Api.NewsPosts;

/// <summary>The single length rule for post text, shared by the Blazor counter, the generator and the publish endpoint.
/// Counts text elements so an emoji or a letter with combining marks counts once.</summary>
public static class PostText {
    public const int MaxLength = 280;

    public static int Length(string text) => new StringInfo(text).LengthInTextElements;

    public static bool IsValid(string? text) {
        if (string.IsNullOrWhiteSpace(text))
            return false;
        return Length(text) <= MaxLength;
    }
}
```

Enums (`Quartermaster.Data/NewsPosts/`), one file each:

```csharp
namespace Quartermaster.Data.NewsPosts;

public enum NewsFeedKind {
    Rss,
    TagesschauApi
}
```

```csharp
namespace Quartermaster.Data.NewsPosts;

/// <summary>Persisted as int; append new values only.</summary>
public enum NewsItemStatus {
    New,
    Irrelevant,
    Relevant,
    Generated,
    Posting,
    Posted,
    Dismissed,
    Failed
}
```

```csharp
namespace Quartermaster.Data.NewsPosts;

public enum PostPublicationStatus {
    Sent,
    Failed
}
```

Entities (one file each, same namespace, `using System; using LinqToDB.Mapping;`):

```csharp
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
```

```csharp
[Table(TableName, IsColumnAttributeRequired = false)]
public class ProgrammeSection {
    public const string TableName = "ProgrammeSections";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SourceId { get; set; }
    public string PageTitle { get; set; } = "";
    public string Heading { get; set; } = "";
    public string HeadingPath { get; set; } = "";
    public string Anchor { get; set; } = "";
    public string Body { get; set; } = "";
    public string ContentHash { get; set; } = "";
    public byte[] Embedding { get; set; } = [];
    public DateTime UpdatedAt { get; set; }
}
```

(`PageTitle` is needed because a prefix source spans many pages; the section link is `https://wiki.piratenpartei.de/{PageTitle}#{Anchor}` with spaces as underscores.)

```csharp
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
}
```

```csharp
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
```

```csharp
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
```

```csharp
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
```

Repositories (`using System; using System.Collections.Generic; using System.Linq; using LinqToDB;`):

```csharp
public class ProgrammeRepository {
    private readonly DbContext _context;

    public ProgrammeRepository(DbContext context) {
        _context = context;
    }

    public List<ProgrammeSource> GetSources()
        => _context.ProgrammeSources.OrderBy(s => s.WikiPageTitle).ToList();

    public ProgrammeSource? GetSource(Guid id)
        => _context.ProgrammeSources.Where(s => s.Id == id).FirstOrDefault();

    public bool SourceExists(string wikiPageTitle)
        => _context.ProgrammeSources.Any(s => s.ChapterId == null && s.WikiPageTitle == wikiPageTitle);

    public void CreateSource(ProgrammeSource source)
        => _context.Insert(source);

    public void DeleteSource(Guid id) {
        _context.ProgrammeSections.Where(s => s.SourceId == id).Delete();
        _context.ProgrammeSources.Where(s => s.Id == id).Delete();
    }

    public void MarkSourceSynced(Guid id, long? revisionId, DateTime now) {
        _context.ProgrammeSources
            .Where(s => s.Id == id)
            .Set(s => s.LastRevisionId, revisionId)
            .Set(s => s.LastSyncedAt, now)
            .Set(s => s.LastError, (string?)null)
            .Update();
    }

    public void MarkSourceFailed(Guid id, string error) {
        _context.ProgrammeSources
            .Where(s => s.Id == id)
            .Set(s => s.LastError, error)
            .Update();
    }

    public List<ProgrammeSection> GetSections(Guid sourceId, string pageTitle)
        => _context.ProgrammeSections.Where(s => s.SourceId == sourceId && s.PageTitle == pageTitle).ToList();

    public List<ProgrammeSection> GetAllSections()
        => _context.ProgrammeSections.ToList();

    public List<ProgrammeSection> GetSectionsByIds(IReadOnlyCollection<Guid> ids)
        => _context.ProgrammeSections.Where(s => ids.Contains(s.Id)).ToList();

    public int CountSections(Guid sourceId)
        => _context.ProgrammeSections.Count(s => s.SourceId == sourceId);

    public List<string> GetPageTitles(Guid sourceId)
        => _context.ProgrammeSections
            .Where(s => s.SourceId == sourceId)
            .Select(s => s.PageTitle)
            .Distinct()
            .ToList();

    /// <summary>Replaces the stored sections of one page: inserts <paramref name="added"/>, updates <paramref name="changed"/>, deletes <paramref name="removedIds"/>. Runs in one transaction so a failure keeps the previous state.</summary>
    public void ApplyPageDiff(List<ProgrammeSection> added, List<ProgrammeSection> changed, List<Guid> removedIds) {
        using var tx = _context.BeginTransaction();
        foreach (var section in added)
            _context.Insert(section);
        foreach (var section in changed)
            _context.Update(section);
        if (removedIds.Count > 0)
            _context.ProgrammeSections.Where(s => removedIds.Contains(s.Id)).Delete();
        tx.Commit();
    }

    public void DeletePage(Guid sourceId, string pageTitle) {
        _context.ProgrammeSections
            .Where(s => s.SourceId == sourceId && s.PageTitle == pageTitle)
            .Delete();
    }
}
```

```csharp
public class NewsFeedRepository {
    private readonly DbContext _context;

    public NewsFeedRepository(DbContext context) {
        _context = context;
    }

    public List<NewsFeed> GetAll()
        => _context.NewsFeeds.OrderBy(f => f.Name).ToList();

    public List<NewsFeed> GetEnabled()
        => _context.NewsFeeds.Where(f => f.IsEnabled).ToList();

    public NewsFeed? Get(Guid id)
        => _context.NewsFeeds.Where(f => f.Id == id).FirstOrDefault();

    public bool ExistsByUrl(string url)
        => _context.NewsFeeds.Any(f => f.Url == url);

    public void Create(NewsFeed feed)
        => _context.Insert(feed);

    public void Update(NewsFeed feed)
        => _context.Update(feed);

    public void Delete(Guid id)
        => _context.NewsFeeds.Where(f => f.Id == id).Delete();

    public void MarkPolled(Guid id, DateTime now, string? error) {
        _context.NewsFeeds
            .Where(f => f.Id == id)
            .Set(f => f.LastPolledAt, now)
            .Set(f => f.LastError, error)
            .Update();
    }
}
```

```csharp
public class NewsItemRepository {
    private readonly DbContext _context;

    public NewsItemRepository(DbContext context) {
        _context = context;
    }

    public NewsItem? Get(Guid id)
        => _context.NewsItems.Where(i => i.Id == id).FirstOrDefault();

    /// <summary>Returns false when the feed already holds an item with the same ExternalId.</summary>
    public bool TryInsert(NewsItem item) {
        if (_context.NewsItems.Any(i => i.FeedId == item.FeedId && i.ExternalId == item.ExternalId))
            return false;
        _context.Insert(item);
        return true;
    }

    public List<NewsItem> GetByStatus(NewsItemStatus status)
        => _context.NewsItems
            .Where(i => i.Status == status)
            .OrderBy(i => i.PublishedAt)
            .ToList();

    public List<NewsItem> GetByStatuses(IReadOnlyCollection<NewsItemStatus> statuses, int limit)
        => _context.NewsItems
            .Where(i => statuses.Contains(i.Status))
            .OrderByDescending(i => i.PublishedAt)
            .Take(limit)
            .ToList();

    public Dictionary<NewsItemStatus, int> CountByStatus()
        => _context.NewsItems
            .GroupBy(i => i.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToList()
            .ToDictionary(x => x.Status, x => x.Count);

    public void Update(NewsItem item) {
        item.UpdatedAt = DateTime.UtcNow;
        _context.Update(item);
    }

    public void SetStatus(Guid id, NewsItemStatus status) {
        _context.NewsItems
            .Where(i => i.Id == id)
            .Set(i => i.Status, status)
            .Set(i => i.UpdatedAt, DateTime.UtcNow)
            .Update();
    }

    /// <summary>Atomically moves a <see cref="NewsItemStatus.Generated"/> item to <see cref="NewsItemStatus.Posting"/>. False means another request already claimed it or it was never generated.</summary>
    public bool TryClaimForPosting(Guid id) {
        var updated = _context.NewsItems
            .Where(i => i.Id == id && i.Status == NewsItemStatus.Generated)
            .Set(i => i.Status, NewsItemStatus.Posting)
            .Set(i => i.UpdatedAt, DateTime.UtcNow)
            .Update();
        return updated == 1;
    }

    public List<PostCandidate> GetCandidates(Guid newsItemId)
        => _context.PostCandidates
            .Where(c => c.NewsItemId == newsItemId)
            .OrderBy(c => c.SortOrder)
            .ToList();

    public List<PostCandidate> GetCandidatesForItems(IReadOnlyCollection<Guid> newsItemIds)
        => _context.PostCandidates
            .Where(c => newsItemIds.Contains(c.NewsItemId))
            .OrderBy(c => c.SortOrder)
            .ToList();

    public void ReplaceCandidates(Guid newsItemId, List<PostCandidate> candidates) {
        using var tx = _context.BeginTransaction();
        _context.PostCandidates.Where(c => c.NewsItemId == newsItemId).Delete();
        var now = DateTime.UtcNow;
        for (var i = 0; i < candidates.Count; i++) {
            candidates[i].NewsItemId = newsItemId;
            candidates[i].SortOrder = i;
            candidates[i].CreatedAt = now;
            _context.Insert(candidates[i]);
        }
        tx.Commit();
    }

    /// <summary>Deletes items (and their candidates) untouched since <paramref name="cutoff"/> that never produced a publication. Returns the number of deleted items.</summary>
    public int DeleteStale(DateTime cutoff) {
        var staleIds = _context.NewsItems
            .Where(i => i.UpdatedAt < cutoff)
            .Where(i => !_context.PostPublications.Any(p => p.NewsItemId == i.Id))
            .Select(i => i.Id)
            .ToList();
        if (staleIds.Count == 0)
            return 0;

        _context.PostCandidates.Where(c => staleIds.Contains(c.NewsItemId)).Delete();
        return _context.NewsItems.Where(i => staleIds.Contains(i.Id)).Delete();
    }
}
```

```csharp
public class PostPublicationRepository {
    private readonly DbContext _context;

    public PostPublicationRepository(DbContext context) {
        _context = context;
    }

    public void Create(PostPublication publication)
        => _context.Insert(publication);

    public List<PostPublication> GetRecent(int limit)
        => _context.PostPublications
            .OrderByDescending(p => p.ApprovedAt)
            .Take(limit)
            .ToList();
}
```

`Quartermaster.Data/NewsPosts/NewsPostsSeeder.cs`:

```csharp
using System.Collections.Generic;

namespace Quartermaster.Data.NewsPosts;

/// <summary>Seeds the V1 federal feeds and programme sources when missing; never touches existing rows so admin edits survive restarts.</summary>
public static class NewsPostsSeeder {
    private static readonly List<NewsFeed> DefaultFeeds = [
        new NewsFeed { Name = "netzpolitik.org", Url = "https://netzpolitik.org/feed/", Kind = NewsFeedKind.Rss, IsEnabled = true },
        new NewsFeed { Name = "Bundestag hib", Url = "https://www.bundestag.de/static/appdata/includes/rss/hib.rss", Kind = NewsFeedKind.Rss, IsEnabled = true },
        new NewsFeed { Name = "tagesschau", Url = "https://www.tagesschau.de/api2u/news/", Kind = NewsFeedKind.TagesschauApi, IsEnabled = true }
    ];

    private static readonly List<string> DefaultSources = [
        "Parteiprogramm",
        "Bundestagswahl 2025/Wahlprogramm",
        "Positionspapiere/"
    ];

    public static void Seed(NewsFeedRepository feedRepo, ProgrammeRepository programmeRepo) {
        foreach (var feed in DefaultFeeds) {
            if (!feedRepo.ExistsByUrl(feed.Url)) {
                feedRepo.Create(new NewsFeed { Name = feed.Name, Url = feed.Url, Kind = feed.Kind, IsEnabled = feed.IsEnabled });
            }
        }
        foreach (var title in DefaultSources) {
            if (!programmeRepo.SourceExists(title))
                programmeRepo.CreateSource(new ProgrammeSource { WikiPageTitle = title });
        }
    }
}
```

`DbContext.cs`: add `using Quartermaster.Data.NewsPosts;`, the tables

```csharp
    public ITable<ProgrammeSource> ProgrammeSources => this.GetTable<ProgrammeSource>();
    public ITable<ProgrammeSection> ProgrammeSections => this.GetTable<ProgrammeSection>();
    public ITable<NewsFeed> NewsFeeds => this.GetTable<NewsFeed>();
    public ITable<NewsItem> NewsItems => this.GetTable<NewsItem>();
    public ITable<PostCandidate> PostCandidates => this.GetTable<PostCandidate>();
    public ITable<PostPublication> PostPublications => this.GetTable<PostPublication>();
```

in `AddRepositories`:

```csharp
        services.AddScoped<ProgrammeRepository>();
        services.AddScoped<NewsFeedRepository>();
        services.AddScoped<NewsItemRepository>();
        services.AddScoped<PostPublicationRepository>();
```

and at the end of `SupplementDefaults`:

```csharp
        NewsPostsSeeder.Seed(
            scope.ServiceProvider.GetRequiredService<NewsFeedRepository>(),
            scope.ServiceProvider.GetRequiredService<ProgrammeRepository>());
```

`M003.cs`: add `using Quartermaster.Data.NewsPosts;`, append to `Up()` after the PdfTemplates index:

```csharp
        Create.Table(ProgrammeSource.TableName)
            .WithColumn(nameof(ProgrammeSource.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(ProgrammeSource.ChapterId)).AsGuid().Nullable()
            .WithColumn(nameof(ProgrammeSource.WikiPageTitle)).AsString(256).NotNullable()
            .WithColumn(nameof(ProgrammeSource.LastSyncedAt)).AsCustom("DATETIME(6)").Nullable()
            .WithColumn(nameof(ProgrammeSource.LastRevisionId)).AsInt64().Nullable()
            .WithColumn(nameof(ProgrammeSource.LastError)).AsCustom("TEXT").Nullable();

        Create.Index("IX_ProgrammeSources_Chapter_Title")
            .OnTable(ProgrammeSource.TableName)
            .OnColumn(nameof(ProgrammeSource.ChapterId)).Ascending()
            .OnColumn(nameof(ProgrammeSource.WikiPageTitle)).Ascending()
            .WithOptions().Unique();

        Create.Table(ProgrammeSection.TableName)
            .WithColumn(nameof(ProgrammeSection.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(ProgrammeSection.SourceId)).AsGuid().NotNullable()
            .WithColumn(nameof(ProgrammeSection.PageTitle)).AsString(256).NotNullable()
            .WithColumn(nameof(ProgrammeSection.Heading)).AsString(512).NotNullable()
            .WithColumn(nameof(ProgrammeSection.HeadingPath)).AsString(1024).NotNullable()
            .WithColumn(nameof(ProgrammeSection.Anchor)).AsString(512).NotNullable()
            .WithColumn(nameof(ProgrammeSection.Body)).AsCustom("MEDIUMTEXT").NotNullable()
            .WithColumn(nameof(ProgrammeSection.ContentHash)).AsString(64).NotNullable()
            .WithColumn(nameof(ProgrammeSection.Embedding)).AsCustom("BLOB").NotNullable()
            .WithColumn(nameof(ProgrammeSection.UpdatedAt)).AsCustom("DATETIME(6)").NotNullable();

        Create.ForeignKey("FK_ProgrammeSections_SourceId")
            .FromTable(ProgrammeSection.TableName).ForeignColumn(nameof(ProgrammeSection.SourceId))
            .ToTable(ProgrammeSource.TableName).PrimaryColumn(nameof(ProgrammeSource.Id));

        Create.Table(NewsFeed.TableName)
            .WithColumn(nameof(NewsFeed.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(NewsFeed.ChapterId)).AsGuid().Nullable()
            .WithColumn(nameof(NewsFeed.Name)).AsString(256).NotNullable()
            .WithColumn(nameof(NewsFeed.Url)).AsString(1024).NotNullable()
            .WithColumn(nameof(NewsFeed.Kind)).AsInt32().NotNullable()
            .WithColumn(nameof(NewsFeed.IsEnabled)).AsBoolean().NotNullable()
            .WithColumn(nameof(NewsFeed.LastPolledAt)).AsCustom("DATETIME(6)").Nullable()
            .WithColumn(nameof(NewsFeed.LastError)).AsCustom("TEXT").Nullable();

        Create.Table(NewsItem.TableName)
            .WithColumn(nameof(NewsItem.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(NewsItem.FeedId)).AsGuid().NotNullable()
            .WithColumn(nameof(NewsItem.ExternalId)).AsString(512).NotNullable()
            .WithColumn(nameof(NewsItem.Url)).AsString(1024).NotNullable()
            .WithColumn(nameof(NewsItem.Title)).AsString(1024).NotNullable()
            .WithColumn(nameof(NewsItem.Teaser)).AsCustom("TEXT").NotNullable()
            .WithColumn(nameof(NewsItem.ArticleText)).AsCustom("MEDIUMTEXT").Nullable()
            .WithColumn(nameof(NewsItem.HasFullText)).AsBoolean().NotNullable()
            .WithColumn(nameof(NewsItem.PublishedAt)).AsCustom("DATETIME(6)").NotNullable()
            .WithColumn(nameof(NewsItem.FetchedAt)).AsCustom("DATETIME(6)").NotNullable()
            .WithColumn(nameof(NewsItem.Status)).AsInt32().NotNullable()
            .WithColumn(nameof(NewsItem.TriageReason)).AsCustom("TEXT").Nullable()
            .WithColumn(nameof(NewsItem.BestSimilarity)).AsDouble().Nullable()
            .WithColumn(nameof(NewsItem.MatchedSectionIds)).AsCustom("TEXT").NotNullable()
            .WithColumn(nameof(NewsItem.FailureCount)).AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn(nameof(NewsItem.LastError)).AsCustom("TEXT").Nullable()
            .WithColumn(nameof(NewsItem.UpdatedAt)).AsCustom("DATETIME(6)").NotNullable();

        Create.ForeignKey("FK_NewsItems_FeedId")
            .FromTable(NewsItem.TableName).ForeignColumn(nameof(NewsItem.FeedId))
            .ToTable(NewsFeed.TableName).PrimaryColumn(nameof(NewsFeed.Id));

        Create.Index("IX_NewsItems_Feed_ExternalId")
            .OnTable(NewsItem.TableName)
            .OnColumn(nameof(NewsItem.FeedId)).Ascending()
            .OnColumn(nameof(NewsItem.ExternalId)).Ascending()
            .WithOptions().Unique();

        Create.Index("IX_NewsItems_Status")
            .OnTable(NewsItem.TableName)
            .OnColumn(nameof(NewsItem.Status));

        Create.Table(PostCandidate.TableName)
            .WithColumn(nameof(PostCandidate.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(PostCandidate.NewsItemId)).AsGuid().NotNullable()
            .WithColumn(nameof(PostCandidate.Text)).AsCustom("TEXT").NotNullable()
            .WithColumn(nameof(PostCandidate.SectionId)).AsGuid().NotNullable()
            .WithColumn(nameof(PostCandidate.SortOrder)).AsInt32().NotNullable()
            .WithColumn(nameof(PostCandidate.CreatedAt)).AsCustom("DATETIME(6)").NotNullable();

        Create.ForeignKey("FK_PostCandidates_NewsItemId")
            .FromTable(PostCandidate.TableName).ForeignColumn(nameof(PostCandidate.NewsItemId))
            .ToTable(NewsItem.TableName).PrimaryColumn(nameof(NewsItem.Id));

        Create.Table(PostPublication.TableName)
            .WithColumn(nameof(PostPublication.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(PostPublication.NewsItemId)).AsGuid().NotNullable()
            .WithColumn(nameof(PostPublication.CandidateId)).AsGuid().Nullable()
            .WithColumn(nameof(PostPublication.FinalText)).AsCustom("TEXT").NotNullable()
            .WithColumn(nameof(PostPublication.ChannelIds)).AsCustom("TEXT").NotNullable()
            .WithColumn(nameof(PostPublication.PostizPostIds)).AsCustom("TEXT").NotNullable()
            .WithColumn(nameof(PostPublication.ScheduledFor)).AsCustom("DATETIME(6)").Nullable()
            .WithColumn(nameof(PostPublication.Status)).AsInt32().NotNullable()
            .WithColumn(nameof(PostPublication.Error)).AsCustom("TEXT").Nullable()
            .WithColumn(nameof(PostPublication.ApprovedByUserId)).AsGuid().NotNullable()
            .WithColumn(nameof(PostPublication.ApprovedAt)).AsCustom("DATETIME(6)").NotNullable();

        Create.ForeignKey("FK_PostPublications_NewsItemId")
            .FromTable(PostPublication.TableName).ForeignColumn(nameof(PostPublication.NewsItemId))
            .ToTable(NewsItem.TableName).PrimaryColumn(nameof(NewsItem.Id));
```

and at the start of `Down()`:

```csharp
        DropTableIfExists(PostPublication.TableName);
        DropTableIfExists(PostCandidate.TableName);
        DropTableIfExists(NewsItem.TableName);
        DropTableIfExists(NewsFeed.TableName);
        DropTableIfExists(ProgrammeSection.TableName);
        DropTableIfExists(ProgrammeSource.TableName);
```

The local dev DB already applied M003, so FluentMigrator will not create the new tables there. Add them additively (never drop the dev DB): after the test run in Step 4 has built `quartermaster_test_w0` from the migrations, copy each new table's definition over:

```bash
for t in ProgrammeSources ProgrammeSections NewsFeeds NewsItems PostCandidates PostPublications; do
  mysql -uroot -N -e "SHOW CREATE TABLE quartermaster_test_w0.$t" | cut -f2 | sed 's/\\n/\n/g' | mysql -uroot quartermaster
done
```

Then confirm with `mysql -uroot quartermaster -e "SHOW TABLES LIKE 'News%'"` and mention the manual step in the task report.

`PermissionIdentifier.cs` (after `EditPdfTemplates`):

```csharp
    /// <summary>Lets the holder see the news-post queue, generate candidates, and greenlight posts to Postiz.</summary>
    public static readonly string ReviewNewsPosts = "newsposts_review";

    /// <summary>Lets the holder manage news feeds and programme sources and trigger the programme sync.</summary>
    public static readonly string ManageNewsPosts = "newsposts_manage";
```

and append both to `SystemPermissions`. `PermissionRepository.SupplementDefaults` (next to the PDF template line):

```csharp
        AddIfNotExists(PermissionIdentifier.ReviewNewsPosts, "Social-Media-Vorschläge prüfen und freigeben", true);
        AddIfNotExists(PermissionIdentifier.ManageNewsPosts, "Social-Media-Quellen verwalten", true);
```

`OptionRepository.SupplementDefaults`, a block at the end:

```csharp
        AddDefinitionIfNotExists("newsposts.enabled",
            "Social Media: Aktiviert",
            OptionDataType.String, false, "", "false",
            description: "Schaltet das Abrufen von Nachrichten und das Erzeugen von Vorschlägen ein (true/false).");

        AddDefinitionIfNotExists("newsposts.poll_interval_minutes",
            "Social Media: Abrufintervall",
            OptionDataType.Number, false, "", "15",
            description: "Intervall in Minuten, in dem die Nachrichtenquellen abgerufen und verarbeitet werden.");

        AddDefinitionIfNotExists("newsposts.max_item_age_hours",
            "Social Media: Maximales Meldungsalter",
            OptionDataType.Number, false, "", "48",
            description: "Ältere Meldungen werden beim Abruf ignoriert (Stunden).");

        AddDefinitionIfNotExists("newsposts.retention_days",
            "Social Media: Aufbewahrung",
            OptionDataType.Number, false, "", "30",
            description: "Meldungen ohne Veröffentlichung werden nach so vielen Tagen ohne Änderung gelöscht.");

        AddDefinitionIfNotExists("newsposts.mistral.api_key",
            "Social Media: Mistral API-Schlüssel",
            OptionDataType.String, false, "", "",
            isSecret: true);

        AddDefinitionIfNotExists("newsposts.mistral.base_url",
            "Social Media: Mistral API-URL",
            OptionDataType.String, false, "", "https://api.mistral.ai");

        AddDefinitionIfNotExists("newsposts.mistral.triage_model",
            "Social Media: Modell für Vorauswahl",
            OptionDataType.String, false, "", "mistral-small-latest");

        AddDefinitionIfNotExists("newsposts.mistral.generation_model",
            "Social Media: Modell für Vorschläge",
            OptionDataType.String, false, "", "mistral-medium-latest");

        AddDefinitionIfNotExists("newsposts.mistral.embedding_model",
            "Social Media: Embedding-Modell",
            OptionDataType.String, false, "", "mistral-embed");

        AddDefinitionIfNotExists("newsposts.triage.min_similarity",
            "Social Media: Mindestähnlichkeit",
            OptionDataType.String, false, "", "0.70",
            description: "Unterhalb dieser Kosinus-Ähnlichkeit zum Programm gilt eine Meldung ohne KI-Aufruf als nicht relevant (0 bis 1).");

        AddDefinitionIfNotExists("newsposts.generation.candidate_count",
            "Social Media: Anzahl Vorschläge",
            OptionDataType.Number, false, "", "5");

        AddDefinitionIfNotExists("newsposts.generation.prompt",
            "Social Media: Stilregeln",
            OptionDataType.String, false, "", NewsPostsDefaultPrompt,
            description: "Ton- und Stilregeln für die erzeugten Beiträge. Format- und Faktenregeln ergänzt die Anwendung selbst.");

        AddDefinitionIfNotExists("newsposts.postiz.base_url",
            "Social Media: Postiz API-URL",
            OptionDataType.String, false, "", "",
            description: "Basis-URL der Postiz-Instanz inklusive /api, z.B. https://postiz.example.org/api");

        AddDefinitionIfNotExists("newsposts.postiz.api_key",
            "Social Media: Postiz API-Schlüssel",
            OptionDataType.String, false, "", "",
            isSecret: true);

        AddDefinitionIfNotExists("newsposts.postiz.x_reply_setting",
            "Social Media: Antworten auf X",
            OptionDataType.String, false, "", "everyone",
            description: "Wer auf X antworten darf: everyone, following, mentionedUsers, subscribers oder verified.");
```

with, as a private constant in `OptionRepository`:

```csharp
    private const string NewsPostsDefaultPrompt =
        "Du schreibst Beiträge für die Social-Media-Kanäle der Piratenpartei Deutschland. " +
        "Klar, pointiert, sachlich im Kern, gern mit einer zugespitzten Pointe. " +
        "Sprich als \"wir PIRATEN\", nicht als Einzelperson. Keine Beleidigungen, keine Unterstellungen. " +
        "Höchstens zwei Hashtags, bevorzugt #Piraten plus ein Themen-Hashtag.";
```

`WorkerDatabase.ExecuteTruncate`: add before the closing `"""`:

```
            TRUNCATE TABLE PostPublications;
            TRUNCATE TABLE PostCandidates;
            TRUNCATE TABLE NewsItems;
            TRUNCATE TABLE NewsFeeds;
            TRUNCATE TABLE ProgrammeSections;
            TRUNCATE TABLE ProgrammeSources;
```

`Configuration.md`: add a section `### News Posts (optional)` after "OpenID Connect", with the option list from the spec's Configuration section in the same `| Identifier | Purpose |` table style the file already uses.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet build Quartermaster.sln -m:2 -v:q -nologo`, then the treenode filter for `PostTextTests`, `NewsItemRepositoryTests`, `OptionRepositoryTests`.
Expected: all pass.

- [ ] **Step 5: Checkpoint** (user commits)

---

### Task 2: Mistral client

**Files:**
- Create: `Quartermaster.Server/NewsPosts/NewsPostsSettings.cs`
- Create: `Quartermaster.Server/NewsPosts/Mistral/IMistralClient.cs`, `HttpMistralClient.cs`, `MistralException.cs`
- Create: `Quartermaster.Server.Tests/NewsPosts/Fakes/StubHttpMessageHandler.cs`
- Test: `Quartermaster.Server.Tests/NewsPosts/HttpMistralClientTests.cs`

**Interfaces:**
- Consumes: `OptionRepository.GetGlobalValue(string)` (Task 1 option ids)
- Produces:
  - `NewsPostsSettings.ReadFrom(OptionRepository)` with properties `Enabled`, `PollInterval` (TimeSpan), `MaxItemAge` (TimeSpan), `Retention` (TimeSpan), `MistralApiKey`, `MistralBaseUrl`, `TriageModel`, `GenerationModel`, `EmbeddingModel`, `MinSimilarity` (double), `CandidateCount` (int), `GenerationPrompt`, `PostizBaseUrl`, `PostizApiKey`, `XReplySetting`, and `bool HasMistral`, `bool HasPostiz`.
  - `IMistralClient.EmbedAsync(IReadOnlyList<string> inputs, CancellationToken ct) : Task<List<float[]>>`
  - `IMistralClient.CompleteJsonAsync(string model, string systemPrompt, string userPrompt, CancellationToken ct) : Task<string>`
  - `MistralException : Exception`

- [ ] **Step 1: Write the failing tests**

`Fakes/StubHttpMessageHandler.cs`:

```csharp
using System.Net;
using System.Text;

namespace Quartermaster.Server.Tests.NewsPosts.Fakes;

public class StubHttpMessageHandler : HttpMessageHandler {
    private readonly Queue<HttpResponseMessage> _responses = new();

    public List<HttpRequestMessage> Requests { get; } = [];
    public List<string> RequestBodies { get; } = [];

    public void Enqueue(HttpStatusCode status, string body, string mediaType = "application/json") {
        _responses.Enqueue(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, mediaType) });
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        Requests.Add(request);
        RequestBodies.Add(request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
        return _responses.Dequeue();
    }
}
```

`HttpMistralClientTests.cs`:

```csharp
using System.Net;
using System.Text.Json;
using Quartermaster.Server.NewsPosts;
using Quartermaster.Server.NewsPosts.Mistral;
using Quartermaster.Server.Tests.NewsPosts.Fakes;

namespace Quartermaster.Server.Tests.NewsPosts;

public class HttpMistralClientTests {
    private static NewsPostsSettings Settings() => new() {
        MistralApiKey = "key",
        MistralBaseUrl = "https://mistral.test",
        EmbeddingModel = "mistral-embed"
    };

    [Test]
    public async Task EmbedAsync_posts_batch_and_returns_vectors_in_input_order() {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, """
            {"data":[{"index":1,"embedding":[0.5,0.5]},{"index":0,"embedding":[1.0,0.0]}],"usage":{"prompt_tokens":4,"total_tokens":4}}
            """);
        var client = new HttpMistralClient(new HttpClient(handler), Settings());

        var vectors = await client.EmbedAsync(["a", "b"], CancellationToken.None);

        await Assert.That(vectors[0]).IsEquivalentTo(new[] { 1.0f, 0.0f });
        await Assert.That(vectors[1]).IsEquivalentTo(new[] { 0.5f, 0.5f });
        await Assert.That(handler.Requests[0].RequestUri!.ToString()).IsEqualTo("https://mistral.test/v1/embeddings");
        await Assert.That(handler.Requests[0].Headers.Authorization!.ToString()).IsEqualTo("Bearer key");
        using var body = JsonDocument.Parse(handler.RequestBodies[0]);
        await Assert.That(body.RootElement.GetProperty("model").GetString()).IsEqualTo("mistral-embed");
    }

    [Test]
    public async Task CompleteJsonAsync_requests_json_mode_and_returns_message_content() {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, """
            {"choices":[{"message":{"role":"assistant","content":"{\"relevant\":true,\"reason\":\"x\"}"}}],"usage":{"prompt_tokens":10,"completion_tokens":5}}
            """);
        var client = new HttpMistralClient(new HttpClient(handler), Settings());

        var content = await client.CompleteJsonAsync("mistral-small-latest", "sys", "user", CancellationToken.None);

        await Assert.That(content).IsEqualTo("{\"relevant\":true,\"reason\":\"x\"}");
        using var body = JsonDocument.Parse(handler.RequestBodies[0]);
        await Assert.That(body.RootElement.GetProperty("response_format").GetProperty("type").GetString()).IsEqualTo("json_object");
        await Assert.That(body.RootElement.GetProperty("messages").GetArrayLength()).IsEqualTo(2);
    }

    [Test]
    public async Task Non_success_status_throws_MistralException_with_status() {
        var handler = new StubHttpMessageHandler();
        for (var i = 0; i < 3; i++)
            handler.Enqueue(HttpStatusCode.TooManyRequests, """{"message":"rate limited"}""");
        var client = new HttpMistralClient(new HttpClient(handler), Settings());

        var ex = await Assert.ThrowsAsync<MistralException>(() => client.CompleteJsonAsync("m", "s", "u", CancellationToken.None));

        await Assert.That(ex!.Message).Contains("429");
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet build Quartermaster.sln -m:2 -v:q -nologo`
Expected: build errors for missing types.

- [ ] **Step 3: Implement**

`NewsPostsSettings.cs`:

```csharp
using System;
using System.Globalization;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.NewsPosts;

public class NewsPostsSettings {
    public bool Enabled { get; init; }
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMinutes(15);
    public TimeSpan MaxItemAge { get; init; } = TimeSpan.FromHours(48);
    public TimeSpan Retention { get; init; } = TimeSpan.FromDays(30);
    public string MistralApiKey { get; init; } = "";
    public string MistralBaseUrl { get; init; } = "https://api.mistral.ai";
    public string TriageModel { get; init; } = "mistral-small-latest";
    public string GenerationModel { get; init; } = "mistral-medium-latest";
    public string EmbeddingModel { get; init; } = "mistral-embed";
    public double MinSimilarity { get; init; } = 0.70;
    public int CandidateCount { get; init; } = 5;
    public string GenerationPrompt { get; init; } = "";
    public string PostizBaseUrl { get; init; } = "";
    public string PostizApiKey { get; init; } = "";
    public string XReplySetting { get; init; } = "everyone";

    public bool HasMistral => !string.IsNullOrWhiteSpace(MistralApiKey);
    public bool HasPostiz => !string.IsNullOrWhiteSpace(PostizBaseUrl) && !string.IsNullOrWhiteSpace(PostizApiKey);

    public static NewsPostsSettings ReadFrom(OptionRepository options) {
        string Value(string id, string fallback) {
            var value = options.GetGlobalValue(id)?.Value;
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        double Number(string id, double fallback) {
            return double.TryParse(Value(id, ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
        }

        return new NewsPostsSettings {
            Enabled = string.Equals(Value("newsposts.enabled", "false"), "true", StringComparison.OrdinalIgnoreCase),
            PollInterval = TimeSpan.FromMinutes(Math.Max(1, Number("newsposts.poll_interval_minutes", 15))),
            MaxItemAge = TimeSpan.FromHours(Math.Max(1, Number("newsposts.max_item_age_hours", 48))),
            Retention = TimeSpan.FromDays(Math.Max(1, Number("newsposts.retention_days", 30))),
            MistralApiKey = Value("newsposts.mistral.api_key", ""),
            MistralBaseUrl = Value("newsposts.mistral.base_url", "https://api.mistral.ai").TrimEnd('/'),
            TriageModel = Value("newsposts.mistral.triage_model", "mistral-small-latest"),
            GenerationModel = Value("newsposts.mistral.generation_model", "mistral-medium-latest"),
            EmbeddingModel = Value("newsposts.mistral.embedding_model", "mistral-embed"),
            MinSimilarity = Number("newsposts.triage.min_similarity", 0.70),
            CandidateCount = (int)Math.Clamp(Number("newsposts.generation.candidate_count", 5), 1, 10),
            GenerationPrompt = Value("newsposts.generation.prompt", ""),
            PostizBaseUrl = Value("newsposts.postiz.base_url", "").TrimEnd('/'),
            PostizApiKey = Value("newsposts.postiz.api_key", ""),
            XReplySetting = Value("newsposts.postiz.x_reply_setting", "everyone")
        };
    }
}
```

`Mistral/MistralException.cs`:

```csharp
using System;

namespace Quartermaster.Server.NewsPosts.Mistral;

public class MistralException : Exception {
    public MistralException(string message) : base(message) { }
}
```

`Mistral/IMistralClient.cs`:

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Quartermaster.Server.NewsPosts.Mistral;

public interface IMistralClient {
    /// <summary>Returns one vector per input, in input order.</summary>
    Task<List<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken ct);

    /// <summary>Runs a chat completion in JSON mode and returns the raw message content.</summary>
    Task<string> CompleteJsonAsync(string model, string systemPrompt, string userPrompt, CancellationToken ct);
}
```

`Mistral/HttpMistralClient.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Quartermaster.Server.NewsPosts.Mistral;

public class HttpMistralClient : IMistralClient {
    private const int MaxAttempts = 3;

    private readonly HttpClient _http;
    private readonly NewsPostsSettings _settings;
    private readonly ILogger _logger;

    public HttpMistralClient(HttpClient http, NewsPostsSettings settings, ILogger? logger = null) {
        _http = http;
        _settings = settings;
        _logger = logger ?? NullLogger.Instance;
    }

    public async Task<List<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken ct) {
        if (inputs.Count == 0)
            return [];

        var body = new { model = _settings.EmbeddingModel, input = inputs };
        using var doc = await PostAsync("/v1/embeddings", body, _settings.EmbeddingModel, ct);
        return doc.RootElement.GetProperty("data")
            .EnumerateArray()
            .OrderBy(e => e.GetProperty("index").GetInt32())
            .Select(e => e.GetProperty("embedding").EnumerateArray().Select(v => v.GetSingle()).ToArray())
            .ToList();
    }

    public async Task<string> CompleteJsonAsync(string model, string systemPrompt, string userPrompt, CancellationToken ct) {
        var body = new {
            model,
            temperature = 0.4,
            response_format = new { type = "json_object" },
            messages = new[] {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        };
        using var doc = await PostAsync("/v1/chat/completions", body, model, ct);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
    }

    private async Task<JsonDocument> PostAsync(string path, object body, string model, CancellationToken ct) {
        for (var attempt = 1; ; attempt++) {
            var sw = Stopwatch.StartNew();
            using var request = new HttpRequestMessage(HttpMethod.Post, _settings.MistralBaseUrl + path) {
                Content = JsonContent.Create(body)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.MistralApiKey);

            using var response = await _http.SendAsync(request, ct);
            var text = await response.Content.ReadAsStringAsync(ct);
            if (response.IsSuccessStatusCode) {
                var doc = JsonDocument.Parse(text);
                LogUsage(path, model, doc, sw.ElapsedMilliseconds);
                return doc;
            }

            var status = (int)response.StatusCode;
            if (!IsRetryable(status) || attempt >= MaxAttempts)
                throw new MistralException($"Mistral {path} returned {status}: {Truncate(text)}");

            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
        }
    }

    private static bool IsRetryable(int status) => status == 429 || status >= 500;

    private void LogUsage(string path, string model, JsonDocument doc, long elapsedMs) {
        var tokens = doc.RootElement.TryGetProperty("usage", out var usage) ? usage.ToString() : "{}";
        _logger.LogInformation("Mistral {Path} model={Model} usage={Usage} durationMs={Duration}", path, model, tokens, elapsedMs);
    }

    private static string Truncate(string text) => text.Length <= 300 ? text : text[..300];
}
```

The two-argument constructor is what the tests use; production wiring (Task 5) also passes a logger. The 429 test waits through the backoff (about 6 seconds).

- [ ] **Step 4: Run tests to verify they pass**

Run: build, then treenode filter `HttpMistralClientTests`.
Expected: 3 passed.

- [ ] **Step 5: Checkpoint** (user commits)

---

### Task 3: Postiz client and request builder

**Files:**
- Create: `Quartermaster.Server/NewsPosts/Postiz/PostizChannel.cs`, `IPostizClient.cs`, `HttpPostizClient.cs`, `PostizRequestBuilder.cs`, `PostizException.cs`
- Test: `Quartermaster.Server.Tests/NewsPosts/PostizRequestBuilderTests.cs`, `Quartermaster.Server.Tests/NewsPosts/HttpPostizClientTests.cs`

**Interfaces:**
- Consumes: `NewsPostsSettings` (Task 2), `StubHttpMessageHandler` (Task 2)
- Produces:
  - `record PostizChannel(string Id, string Name, string Identifier, string? Profile, string? Picture, bool Disabled)`
  - `IPostizClient.GetChannelsAsync(CancellationToken ct) : Task<List<PostizChannel>>`
  - `IPostizClient.CreatePostAsync(string text, IReadOnlyList<PostizChannel> channels, DateTime? scheduledForUtc, CancellationToken ct) : Task<List<string>>` (returns Postiz post ids)
  - `PostizRequestBuilder.Build(string text, IReadOnlyList<PostizChannel> channels, DateTime? scheduledForUtc, DateTime nowUtc, string xReplySetting) : JsonObject`
  - `PostizException : Exception`

- [ ] **Step 1: Write the failing tests**

`PostizRequestBuilderTests.cs`:

```csharp
using Quartermaster.Server.NewsPosts.Postiz;

namespace Quartermaster.Server.Tests.NewsPosts;

public class PostizRequestBuilderTests {
    private static readonly PostizChannel X = new("int-x", "Piraten X", "x", "@piraten", null, false);
    private static readonly PostizChannel Mastodon = new("int-m", "Piraten Mastodon", "mastodon", "@piraten@social", null, false);

    [Test]
    public async Task Now_post_has_one_entry_per_channel_with_platform_settings() {
        var now = new DateTime(2026, 9, 23, 10, 0, 0, DateTimeKind.Utc);

        var json = PostizRequestBuilder.Build("Hallo", [X, Mastodon], null, now, "everyone");

        await Assert.That(json["type"]!.GetValue<string>()).IsEqualTo("now");
        await Assert.That(json["shortLink"]!.GetValue<bool>()).IsFalse();
        var posts = json["posts"]!.AsArray();
        await Assert.That(posts.Count).IsEqualTo(2);
        await Assert.That(posts[0]!["integration"]!["id"]!.GetValue<string>()).IsEqualTo("int-x");
        await Assert.That(posts[0]!["value"]![0]!["content"]!.GetValue<string>()).IsEqualTo("Hallo");
        await Assert.That(posts[0]!["settings"]!["__type"]!.GetValue<string>()).IsEqualTo("x");
        await Assert.That(posts[0]!["settings"]!["who_can_reply_post"]!.GetValue<string>()).IsEqualTo("everyone");
        await Assert.That(posts[1]!["settings"]!["__type"]!.GetValue<string>()).IsEqualTo("mastodon");
        await Assert.That(posts[1]!["settings"]!.AsObject().ContainsKey("who_can_reply_post")).IsFalse();
    }

    [Test]
    public async Task Scheduled_post_carries_utc_iso_date() {
        var now = new DateTime(2026, 9, 23, 10, 0, 0, DateTimeKind.Utc);
        var at = new DateTime(2026, 9, 24, 8, 30, 0, DateTimeKind.Utc);

        var json = PostizRequestBuilder.Build("Hallo", [Mastodon], at, now, "everyone");

        await Assert.That(json["type"]!.GetValue<string>()).IsEqualTo("schedule");
        await Assert.That(json["date"]!.GetValue<string>()).IsEqualTo("2026-09-24T08:30:00.000Z");
    }
}
```

`HttpPostizClientTests.cs`:

```csharp
using System.Net;
using Quartermaster.Server.NewsPosts;
using Quartermaster.Server.NewsPosts.Postiz;
using Quartermaster.Server.Tests.NewsPosts.Fakes;

namespace Quartermaster.Server.Tests.NewsPosts;

public class HttpPostizClientTests {
    private static NewsPostsSettings Settings() => new() { PostizBaseUrl = "https://postiz.test/api", PostizApiKey = "pk" };

    [Test]
    public async Task GetChannelsAsync_maps_integrations_and_sends_raw_key() {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, """
            [{"id":"a","name":"Piraten X","identifier":"x","picture":"p","disabled":false,"profile":"piraten","customer":null},
             {"id":"b","name":"Alt","identifier":"bluesky","picture":null,"disabled":true,"profile":"alt"}]
            """);
        var client = new HttpPostizClient(new HttpClient(handler), Settings());

        var channels = await client.GetChannelsAsync(CancellationToken.None);

        await Assert.That(channels.Count).IsEqualTo(2);
        await Assert.That(channels[1].Disabled).IsTrue();
        await Assert.That(handler.Requests[0].RequestUri!.ToString()).IsEqualTo("https://postiz.test/api/public/v1/integrations");
        await Assert.That(handler.Requests[0].Headers.GetValues("Authorization").Single()).IsEqualTo("pk");
    }

    [Test]
    public async Task CreatePostAsync_returns_post_ids() {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.Created, """[{"postId":"p1","integration":"a"},{"postId":"p2","integration":"b"}]""");
        var client = new HttpPostizClient(new HttpClient(handler), Settings());
        var channels = new List<PostizChannel> {
            new("a", "X", "x", null, null, false),
            new("b", "M", "mastodon", null, null, false)
        };

        var ids = await client.CreatePostAsync("Text", channels, null, CancellationToken.None);

        await Assert.That(ids).IsEquivalentTo(new List<string> { "p1", "p2" });
        await Assert.That(handler.Requests[0].RequestUri!.ToString()).IsEqualTo("https://postiz.test/api/public/v1/posts");
    }

    [Test]
    public async Task CreatePostAsync_throws_PostizException_on_error() {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.BadRequest, """{"message":"invalid"}""");
        var client = new HttpPostizClient(new HttpClient(handler), Settings());

        var ex = await Assert.ThrowsAsync<PostizException>(() =>
            client.CreatePostAsync("Text", [new PostizChannel("a", "X", "x", null, null, false)], null, CancellationToken.None));

        await Assert.That(ex!.Message).Contains("400");
    }
}
```

- [ ] **Step 2: Run to verify failure** (build errors for missing types)

- [ ] **Step 3: Implement**

`Postiz/PostizChannel.cs`:

```csharp
namespace Quartermaster.Server.NewsPosts.Postiz;

public record PostizChannel(string Id, string Name, string Identifier, string? Profile, string? Picture, bool Disabled);
```

`Postiz/PostizException.cs`:

```csharp
using System;

namespace Quartermaster.Server.NewsPosts.Postiz;

public class PostizException : Exception {
    public PostizException(string message) : base(message) { }
}
```

`Postiz/IPostizClient.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Quartermaster.Server.NewsPosts.Postiz;

public interface IPostizClient {
    Task<List<PostizChannel>> GetChannelsAsync(CancellationToken ct);

    /// <summary>Creates one post per channel with the same text. <paramref name="scheduledForUtc"/> null means publish now. Returns the Postiz post ids.</summary>
    Task<List<string>> CreatePostAsync(string text, IReadOnlyList<PostizChannel> channels, DateTime? scheduledForUtc, CancellationToken ct);
}
```

`Postiz/PostizRequestBuilder.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Nodes;

namespace Quartermaster.Server.NewsPosts.Postiz;

public static class PostizRequestBuilder {
    public static JsonObject Build(string text, IReadOnlyList<PostizChannel> channels, DateTime? scheduledForUtc, DateTime nowUtc, string xReplySetting) {
        var posts = new JsonArray();
        foreach (var channel in channels) {
            var settings = new JsonObject { ["__type"] = channel.Identifier };
            if (channel.Identifier == "x")
                settings["who_can_reply_post"] = xReplySetting;

            posts.Add(new JsonObject {
                ["integration"] = new JsonObject { ["id"] = channel.Id },
                ["value"] = new JsonArray(new JsonObject { ["content"] = text, ["image"] = new JsonArray() }),
                ["settings"] = settings
            });
        }

        var date = scheduledForUtc ?? nowUtc;
        return new JsonObject {
            ["type"] = scheduledForUtc == null ? "now" : "schedule",
            ["date"] = date.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
            ["shortLink"] = false,
            ["tags"] = new JsonArray(),
            ["posts"] = posts
        };
    }
}
```

`Postiz/HttpPostizClient.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Quartermaster.Server.NewsPosts.Postiz;

public class HttpPostizClient : IPostizClient {
    private readonly HttpClient _http;
    private readonly NewsPostsSettings _settings;

    public HttpPostizClient(HttpClient http, NewsPostsSettings settings) {
        _http = http;
        _settings = settings;
    }

    public async Task<List<PostizChannel>> GetChannelsAsync(CancellationToken ct) {
        using var request = CreateRequest(HttpMethod.Get, "/public/v1/integrations");
        using var doc = await SendAsync(request, ct);
        return doc.RootElement
            .EnumerateArray()
            .Select(e => new PostizChannel(
                e.GetProperty("id").GetString() ?? "",
                StringOrNull(e, "name") ?? "",
                StringOrNull(e, "identifier") ?? "",
                StringOrNull(e, "profile"),
                StringOrNull(e, "picture"),
                e.TryGetProperty("disabled", out var disabled) && disabled.ValueKind == JsonValueKind.True))
            .ToList();
    }

    public async Task<List<string>> CreatePostAsync(string text, IReadOnlyList<PostizChannel> channels, DateTime? scheduledForUtc, CancellationToken ct) {
        var body = PostizRequestBuilder.Build(text, channels, scheduledForUtc, DateTime.UtcNow, _settings.XReplySetting);
        using var request = CreateRequest(HttpMethod.Post, "/public/v1/posts");
        request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var doc = await SendAsync(request, ct);
        return doc.RootElement
            .EnumerateArray()
            .Select(e => e.GetProperty("postId").GetString() ?? "")
            .ToList();
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path) {
        var request = new HttpRequestMessage(method, _settings.PostizBaseUrl + path);
        request.Headers.TryAddWithoutValidation("Authorization", _settings.PostizApiKey);
        return request;
    }

    private async Task<JsonDocument> SendAsync(HttpRequestMessage request, CancellationToken ct) {
        using var response = await _http.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new PostizException($"Postiz {request.RequestUri?.AbsolutePath} returned {(int)response.StatusCode}: {(text.Length <= 300 ? text : text[..300])}");
        return JsonDocument.Parse(text);
    }

    private static string? StringOrNull(JsonElement element, string name) {
        if (!element.TryGetProperty(name, out var value))
            return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }
}
```

- [ ] **Step 4: Run tests** (`PostizRequestBuilderTests`, `HttpPostizClientTests`): all pass.

- [ ] **Step 5: Checkpoint** (user commits)

---

### Task 4: Wikitext section splitter and markup stripper

**Files:**
- Create: `Quartermaster.Server/NewsPosts/Programme/WikiSection.cs`, `WikiMarkupStripper.cs`, `WikitextSectionSplitter.cs`
- Test: `Quartermaster.Server.Tests/NewsPosts/WikitextSectionSplitterTests.cs`

**Interfaces:**
- Produces:
  - `record WikiSection(string Heading, string HeadingPath, string Anchor, string Body)`
  - `WikitextSectionSplitter.Split(string pageTitle, string wikitext) : List<WikiSection>`
  - `WikiMarkupStripper.Strip(string wikitext) : string`

- [ ] **Step 1: Write the failing tests**

```csharp
using Quartermaster.Server.NewsPosts.Programme;

namespace Quartermaster.Server.Tests.NewsPosts;

public class WikitextSectionSplitterTests {
    private const string Sample = """
        {{Offiziell}}
        <onlyinclude>Einleitung ohne Überschrift</onlyinclude>
        ==Freiheit und Grundrechte==
        === Privatsphäre und Datenschutz ===
        ====Privatsphäre====
        Jedem Bürger muss das Recht auf [[Anonymität|anonyme]] Kommunikation '''garantiert''' werden.<ref>Quelle</ref>
        ====Leer====
        === Jugendschutz ===
        Ein wirksamer Jugendschutz kann nicht durch {{Vorlage|x}} Verbote erreicht werden. Siehe [https://example.org Beispiel].
        """;

    [Test]
    public async Task Splits_at_headings_and_builds_heading_path() {
        var sections = WikitextSectionSplitter.Split("Parteiprogramm", Sample);

        await Assert.That(sections.Select(s => s.Heading).ToList())
            .IsEquivalentTo(new List<string> { "Privatsphäre", "Jugendschutz" });
        await Assert.That(sections[0].HeadingPath)
            .IsEqualTo("Parteiprogramm > Freiheit und Grundrechte > Privatsphäre und Datenschutz > Privatsphäre");
        await Assert.That(sections[1].HeadingPath)
            .IsEqualTo("Parteiprogramm > Freiheit und Grundrechte > Jugendschutz");
    }

    [Test]
    public async Task Strips_links_templates_bold_and_refs() {
        var sections = WikitextSectionSplitter.Split("Parteiprogramm", Sample);

        await Assert.That(sections[0].Body).IsEqualTo("Jedem Bürger muss das Recht auf anonyme Kommunikation garantiert werden.");
        await Assert.That(sections[1].Body).IsEqualTo("Ein wirksamer Jugendschutz kann nicht durch Verbote erreicht werden. Siehe Beispiel.");
    }

    [Test]
    public async Task Anchor_uses_mediawiki_format() {
        var sections = WikitextSectionSplitter.Split("Parteiprogramm", Sample);

        await Assert.That(sections[1].Anchor).IsEqualTo("Jugendschutz");
        await Assert.That(WikitextSectionSplitter.ToAnchor("Privatsphäre und Datenschutz")).IsEqualTo("Privatsphäre_und_Datenschutz");
    }

    [Test]
    public async Task Duplicate_heading_paths_are_disambiguated() {
        var sections = WikitextSectionSplitter.Split("P", "==A==\nx\n==A==\ny");

        await Assert.That(sections.Select(s => s.HeadingPath).ToList())
            .IsEquivalentTo(new List<string> { "P > A", "P > A (2)" });
    }

    [Test]
    public async Task Parent_sections_with_own_text_are_kept() {
        var sections = WikitextSectionSplitter.Split("P", "==A==\nText A\n===B===\nText B");

        await Assert.That(sections.Select(s => s.HeadingPath).ToList())
            .IsEquivalentTo(new List<string> { "P > A", "P > A > B" });
    }
}
```

- [ ] **Step 2: Run to verify failure** (build errors)

- [ ] **Step 3: Implement**

`WikiSection.cs`:

```csharp
namespace Quartermaster.Server.NewsPosts.Programme;

public record WikiSection(string Heading, string HeadingPath, string Anchor, string Body);
```

`WikiMarkupStripper.cs`:

```csharp
using System.Text.RegularExpressions;

namespace Quartermaster.Server.NewsPosts.Programme;

public static partial class WikiMarkupStripper {
    public static string Strip(string wikitext) {
        var text = RefRegex().Replace(wikitext, "");
        text = CommentRegex().Replace(text, "");
        text = RemoveTemplates(text);
        text = TagRegex().Replace(text, "");
        text = PipedLinkRegex().Replace(text, "$1");
        text = PlainLinkRegex().Replace(text, "$1");
        text = ExternalLinkWithLabelRegex().Replace(text, "$1");
        text = ExternalLinkBareRegex().Replace(text, "");
        text = text.Replace("'''", "").Replace("''", "");
        text = ListMarkerRegex().Replace(text, "");
        text = WhitespaceRegex().Replace(text, " ");
        return SpaceBeforePunctuationRegex().Replace(text, "$1").Trim();
    }

    /// <summary>Templates nest (<c>{{a|{{b}}}}</c>), which a single regex cannot match, so they are removed innermost-first until none remain.</summary>
    private static string RemoveTemplates(string text) {
        string previous;
        do {
            previous = text;
            text = InnermostTemplateRegex().Replace(text, "");
        } while (text != previous);
        return text;
    }

    [GeneratedRegex(@"<ref[^>]*/>|<ref[^>]*>.*?</ref>", RegexOptions.Singleline)]
    private static partial Regex RefRegex();

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex CommentRegex();

    [GeneratedRegex(@"\{\{[^{}]*\}\}")]
    private static partial Regex InnermostTemplateRegex();

    [GeneratedRegex(@"</?[a-zA-Z][^>]*>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\[\[(?:[^|\]]*\|)([^\]]*)\]\]")]
    private static partial Regex PipedLinkRegex();

    [GeneratedRegex(@"\[\[([^\]]*)\]\]")]
    private static partial Regex PlainLinkRegex();

    [GeneratedRegex(@"\[https?://\S+\s+([^\]]+)\]")]
    private static partial Regex ExternalLinkWithLabelRegex();

    [GeneratedRegex(@"\[https?://[^\]\s]+\]")]
    private static partial Regex ExternalLinkBareRegex();

    [GeneratedRegex(@"^[*#:;]+\s*", RegexOptions.Multiline)]
    private static partial Regex ListMarkerRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\s+([.,;:!?])")]
    private static partial Regex SpaceBeforePunctuationRegex();
}
```

`WikitextSectionSplitter.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Quartermaster.Server.NewsPosts.Programme;

public static partial class WikitextSectionSplitter {
    public static List<WikiSection> Split(string pageTitle, string wikitext) {
        var sections = new List<WikiSection>();
        var headingStack = new List<(int Level, string Heading)>();
        string? currentHeading = null;
        var body = new StringBuilder();

        void Flush() {
            if (currentHeading == null)
                return;
            var text = WikiMarkupStripper.Strip(body.ToString());
            if (text.Length > 0) {
                var path = string.Join(" > ", new[] { pageTitle }.Concat(headingStack.Select(h => h.Heading)));
                sections.Add(new WikiSection(currentHeading, path, ToAnchor(currentHeading), text));
            }
            body.Clear();
        }

        foreach (var line in wikitext.Split('\n')) {
            var match = HeadingRegex().Match(line.Trim());
            if (!match.Success) {
                body.AppendLine(line);
                continue;
            }

            Flush();
            var level = match.Groups[1].Value.Length;
            var heading = WikiMarkupStripper.Strip(match.Groups[2].Value);
            headingStack.RemoveAll(h => h.Level >= level);
            headingStack.Add((level, heading));
            currentHeading = heading;
        }
        Flush();

        return Disambiguate(sections);
    }

    /// <summary>The sync keys stored sections by heading path, so repeated headings on one page need distinct paths.</summary>
    private static List<WikiSection> Disambiguate(List<WikiSection> sections) {
        var seen = new Dictionary<string, int>();
        var result = new List<WikiSection>();
        foreach (var section in sections) {
            var count = seen.GetValueOrDefault(section.HeadingPath) + 1;
            seen[section.HeadingPath] = count;
            result.Add(count == 1 ? section : section with { HeadingPath = $"{section.HeadingPath} ({count})" });
        }
        return result;
    }

    public static string ToAnchor(string heading) => heading.Trim().Replace(' ', '_');

    [GeneratedRegex(@"^(={2,6})\s*(.+?)\s*\1\s*$")]
    private static partial Regex HeadingRegex();
}
```

- [ ] **Step 4: Run tests** (`WikitextSectionSplitterTests`): all pass. If the "Strips_links..." expectation differs only in whitespace or punctuation spacing, fix the stripper, not the test.

- [ ] **Step 5: Checkpoint** (user commits)

---

### Task 5: Programme index, wiki client and programme sync

**Files:**
- Create: `Quartermaster.Server/NewsPosts/Programme/EmbeddingCodec.cs`, `SectionMatch.cs`, `ProgrammeIndex.cs`, `WikiPage.cs`, `IWikiClient.cs`, `HttpWikiClient.cs`, `ProgrammeSyncService.cs`, `ProgrammeSyncHostedService.cs`
- Create: `Quartermaster.Server.Tests/NewsPosts/Fakes/FakeMistralClient.cs`, `Fakes/FakeWikiClient.cs`
- Modify: `Quartermaster.Server/Program.cs` (registrations)
- Test: `Quartermaster.Server.Tests/NewsPosts/ProgrammeIndexTests.cs`, `ProgrammeSyncServiceTests.cs`

**Interfaces:**
- Consumes: `ProgrammeRepository` (Task 1), `IMistralClient` (Task 2), `WikitextSectionSplitter` (Task 4)
- Produces:
  - `EmbeddingCodec.Encode(float[]) : byte[]`, `EmbeddingCodec.Decode(byte[]) : float[]`
  - `record SectionMatch(Guid SectionId, double Similarity)`
  - `ProgrammeIndex` (singleton): `Reload(IEnumerable<ProgrammeSection>)`, `TopMatches(float[] query, int count) : List<SectionMatch>`, `int Count`, `static double Cosine(float[] a, float[] b)`
  - `record WikiPage(string Title, long RevisionId, string Wikitext)`
  - `IWikiClient.GetPageAsync(string title, CancellationToken ct) : Task<WikiPage>`; `IWikiClient.ListSubpagesAsync(string prefix, CancellationToken ct) : Task<List<string>>`
  - `ProgrammeSyncService.SyncAllAsync(CancellationToken ct) : Task` and `ProgrammeSyncService.LoadIndex()`

- [ ] **Step 1: Write the failing tests**

`Fakes/FakeMistralClient.cs`:

```csharp
using Quartermaster.Server.NewsPosts.Mistral;

namespace Quartermaster.Server.Tests.NewsPosts.Fakes;

/// <summary>Deterministic embeddings: each input maps to a 3-dimensional vector chosen by keyword, so tests control similarity. Chat answers are queued per test.</summary>
public class FakeMistralClient : IMistralClient {
    public int EmbedCalls { get; private set; }
    public List<string> EmbeddedInputs { get; } = [];
    public Queue<string> ChatAnswers { get; } = new();
    public List<(string Model, string System, string User)> ChatCalls { get; } = [];

    public Task<List<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken ct) {
        EmbedCalls++;
        EmbeddedInputs.AddRange(inputs);
        return Task.FromResult(inputs.Select(Vector).ToList());
    }

    public Task<string> CompleteJsonAsync(string model, string systemPrompt, string userPrompt, CancellationToken ct) {
        ChatCalls.Add((model, systemPrompt, userPrompt));
        if (ChatAnswers.Count == 0)
            throw new MistralException("no answer queued");
        return Task.FromResult(ChatAnswers.Dequeue());
    }

    public static float[] Vector(string text) {
        if (text.Contains("Jugendschutz") || text.Contains("Alterskontrolle"))
            return [1f, 0f, 0f];
        if (text.Contains("ÖPNV") || text.Contains("Fahrschein"))
            return [0f, 1f, 0f];
        return [0f, 0f, 1f];
    }
}
```

`Fakes/FakeWikiClient.cs`:

```csharp
using Quartermaster.Server.NewsPosts.Programme;

namespace Quartermaster.Server.Tests.NewsPosts.Fakes;

public class FakeWikiClient : IWikiClient {
    public Dictionary<string, WikiPage> Pages { get; } = [];
    public HashSet<string> Failing { get; } = [];

    public Task<WikiPage> GetPageAsync(string title, CancellationToken ct) {
        if (Failing.Contains(title))
            throw new HttpRequestException("wiki down");
        return Task.FromResult(Pages[title]);
    }

    public Task<List<string>> ListSubpagesAsync(string prefix, CancellationToken ct)
        => Task.FromResult(Pages.Keys.Where(k => k.StartsWith(prefix)).OrderBy(k => k).ToList());
}
```

`ProgrammeIndexTests.cs`:

```csharp
using Quartermaster.Data.NewsPosts;
using Quartermaster.Server.NewsPosts.Programme;

namespace Quartermaster.Server.Tests.NewsPosts;

public class ProgrammeIndexTests {
    private static ProgrammeSection Section(float[] vector) => new() { Embedding = EmbeddingCodec.Encode(vector) };

    [Test]
    public async Task Codec_round_trips() {
        var vector = new[] { 0.25f, -1.5f, 3f };
        await Assert.That(EmbeddingCodec.Decode(EmbeddingCodec.Encode(vector))).IsEquivalentTo(vector);
    }

    [Test]
    public async Task TopMatches_orders_by_cosine_and_limits_count() {
        var a = Section([1f, 0f]);
        var b = Section([0.7f, 0.7f]);
        var c = Section([0f, 1f]);
        var index = new ProgrammeIndex();
        index.Reload([a, b, c]);

        var matches = index.TopMatches([1f, 0.1f], 2);

        await Assert.That(matches.Select(m => m.SectionId).ToList()).IsEquivalentTo(new List<Guid> { a.Id, b.Id });
        await Assert.That(matches[0].Similarity).IsGreaterThan(matches[1].Similarity);
    }

    [Test]
    public async Task Empty_index_returns_no_matches() {
        await Assert.That(new ProgrammeIndex().TopMatches([1f], 8)).IsEmpty();
    }
}
```

`ProgrammeSyncServiceTests.cs`:

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using Quartermaster.Data.NewsPosts;
using Quartermaster.Server.NewsPosts.Programme;
using Quartermaster.Server.Tests.Infrastructure;
using Quartermaster.Server.Tests.NewsPosts.Fakes;

namespace Quartermaster.Server.Tests.NewsPosts;

public class ProgrammeSyncServiceTests : RepositoryTestBase {
    private ProgrammeRepository _repo = default!;
    private FakeWikiClient _wiki = default!;
    private FakeMistralClient _mistral = default!;
    private ProgrammeIndex _index = default!;
    private ProgrammeSyncService _service = default!;

    [Before(Test)]
    public void Setup() {
        _repo = new ProgrammeRepository(Db);
        _wiki = new FakeWikiClient();
        _mistral = new FakeMistralClient();
        _index = new ProgrammeIndex();
        _service = new ProgrammeSyncService(_repo, _wiki, _mistral, _index, NullLogger<ProgrammeSyncService>.Instance);
    }

    [Test]
    public async Task First_sync_stores_sections_embeds_them_and_loads_index() {
        _repo.CreateSource(new ProgrammeSource { WikiPageTitle = "Parteiprogramm" });
        _wiki.Pages["Parteiprogramm"] = new WikiPage("Parteiprogramm", 10, "==Jugendschutz==\nJugendschutz durch Aufklärung.\n==Verkehr==\nÖPNV fahrscheinfrei.");

        await _service.SyncAllAsync(CancellationToken.None);

        await Assert.That(_repo.GetAllSections().Count).IsEqualTo(2);
        await Assert.That(_mistral.EmbeddedInputs.Count).IsEqualTo(2);
        await Assert.That(_index.Count).IsEqualTo(2);
        await Assert.That(_repo.GetSources()[0].LastRevisionId).IsEqualTo(10);
    }

    [Test]
    public async Task Unchanged_revision_is_skipped() {
        _repo.CreateSource(new ProgrammeSource { WikiPageTitle = "Parteiprogramm" });
        _wiki.Pages["Parteiprogramm"] = new WikiPage("Parteiprogramm", 10, "==A==\nText");
        await _service.SyncAllAsync(CancellationToken.None);
        var callsAfterFirst = _mistral.EmbedCalls;

        await _service.SyncAllAsync(CancellationToken.None);

        await Assert.That(_mistral.EmbedCalls).IsEqualTo(callsAfterFirst);
    }

    [Test]
    public async Task Only_changed_sections_are_re_embedded_and_removed_ones_deleted() {
        _repo.CreateSource(new ProgrammeSource { WikiPageTitle = "P" });
        _wiki.Pages["P"] = new WikiPage("P", 1, "==A==\nAlt A\n==B==\nText B\n==C==\nText C");
        await _service.SyncAllAsync(CancellationToken.None);
        _mistral.EmbeddedInputs.Clear();

        _wiki.Pages["P"] = new WikiPage("P", 2, "==A==\nNeu A\n==B==\nText B");
        await _service.SyncAllAsync(CancellationToken.None);

        await Assert.That(_mistral.EmbeddedInputs.Count).IsEqualTo(1);
        await Assert.That(_repo.GetAllSections().Select(s => s.Heading).OrderBy(h => h).ToList())
            .IsEquivalentTo(new List<string> { "A", "B" });
    }

    [Test]
    public async Task Prefix_source_syncs_every_subpage() {
        _repo.CreateSource(new ProgrammeSource { WikiPageTitle = "Positionspapiere/" });
        _wiki.Pages["Positionspapiere/Eins"] = new WikiPage("Positionspapiere/Eins", 1, "==A==\nText");
        _wiki.Pages["Positionspapiere/Zwei"] = new WikiPage("Positionspapiere/Zwei", 1, "==B==\nText");

        await _service.SyncAllAsync(CancellationToken.None);

        await Assert.That(_repo.GetAllSections().Select(s => s.PageTitle).Distinct().Count()).IsEqualTo(2);
    }

    [Test]
    public async Task Failing_page_keeps_old_sections_and_records_error() {
        _repo.CreateSource(new ProgrammeSource { WikiPageTitle = "P" });
        _wiki.Pages["P"] = new WikiPage("P", 1, "==A==\nText");
        await _service.SyncAllAsync(CancellationToken.None);

        _wiki.Failing.Add("P");
        await _service.SyncAllAsync(CancellationToken.None);

        await Assert.That(_repo.GetAllSections().Count).IsEqualTo(1);
        await Assert.That(_repo.GetSources()[0].LastError).Contains("wiki down");
    }
}
```

- [ ] **Step 2: Run to verify failure** (build errors)

- [ ] **Step 3: Implement**

`EmbeddingCodec.cs`:

```csharp
using System;
using System.Runtime.InteropServices;

namespace Quartermaster.Server.NewsPosts.Programme;

public static class EmbeddingCodec {
    public static byte[] Encode(float[] vector) => MemoryMarshal.AsBytes(vector.AsSpan()).ToArray();

    public static float[] Decode(byte[] bytes) => MemoryMarshal.Cast<byte, float>(bytes.AsSpan()).ToArray();
}
```

`SectionMatch.cs`:

```csharp
using System;

namespace Quartermaster.Server.NewsPosts.Programme;

public record SectionMatch(Guid SectionId, double Similarity);
```

`ProgrammeIndex.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Quartermaster.Data.NewsPosts;

namespace Quartermaster.Server.NewsPosts.Programme;

/// <summary>In-memory cosine index over all programme section embeddings. A few hundred vectors make a linear scan cheaper than any vector store.</summary>
public class ProgrammeIndex {
    private readonly Lock _lock = new();
    private List<(Guid Id, float[] Vector)> entries = [];

    public int Count {
        get {
            lock (_lock) {
                return entries.Count;
            }
        }
    }

    public void Reload(IEnumerable<ProgrammeSection> sections) {
        var loaded = sections
            .Where(s => s.Embedding.Length > 0)
            .Select(s => (s.Id, EmbeddingCodec.Decode(s.Embedding)))
            .ToList();
        lock (_lock) {
            entries = loaded;
        }
    }

    public List<SectionMatch> TopMatches(float[] query, int count) {
        List<(Guid Id, float[] Vector)> snapshot;
        lock (_lock) {
            snapshot = entries;
        }
        return snapshot
            .Select(e => new SectionMatch(e.Id, Cosine(query, e.Vector)))
            .OrderByDescending(m => m.Similarity)
            .Take(count)
            .ToList();
    }

    public static double Cosine(float[] a, float[] b) {
        var length = Math.Min(a.Length, b.Length);
        double dot = 0;
        double normA = 0;
        double normB = 0;
        for (var i = 0; i < length; i++) {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        if (normA == 0 || normB == 0)
            return 0;
        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}
```

`WikiPage.cs`:

```csharp
namespace Quartermaster.Server.NewsPosts.Programme;

public record WikiPage(string Title, long RevisionId, string Wikitext);
```

`IWikiClient.cs`:

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Quartermaster.Server.NewsPosts.Programme;

public interface IWikiClient {
    Task<WikiPage> GetPageAsync(string title, CancellationToken ct);

    /// <summary>Lists main-namespace page titles starting with <paramref name="prefix"/> (e.g. <c>Positionspapiere/</c>).</summary>
    Task<List<string>> ListSubpagesAsync(string prefix, CancellationToken ct);
}
```

`HttpWikiClient.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Quartermaster.Server.NewsPosts.Programme;

public class HttpWikiClient : IWikiClient {
    private const string ApiUrl = "https://wiki.piratenpartei.de/api.php";

    /// <summary>The wiki's Anubis bot check challenges browser-like user agents, also on api.php; a plain tool UA gets JSON.</summary>
    private const string UserAgent = "Quartermaster/3 (+https://piratenpartei.de)";

    private readonly HttpClient _http;

    public HttpWikiClient(HttpClient http) {
        _http = http;
    }

    public async Task<WikiPage> GetPageAsync(string title, CancellationToken ct) {
        var url = $"{ApiUrl}?action=parse&page={Uri.EscapeDataString(title)}&prop=wikitext%7Crevid&format=json";
        using var doc = await GetJsonAsync(url, ct);
        if (doc.RootElement.TryGetProperty("error", out var error))
            throw new HttpRequestException($"Wiki error for '{title}': {error.GetProperty("info").GetString()}");

        var parse = doc.RootElement.GetProperty("parse");
        return new WikiPage(
            parse.GetProperty("title").GetString() ?? title,
            parse.GetProperty("revid").GetInt64(),
            parse.GetProperty("wikitext").GetProperty("*").GetString() ?? "");
    }

    public async Task<List<string>> ListSubpagesAsync(string prefix, CancellationToken ct) {
        var titles = new List<string>();
        string? continueFrom = null;
        do {
            var url = $"{ApiUrl}?action=query&list=allpages&apnamespace=0&aplimit=max&format=json&apprefix={Uri.EscapeDataString(prefix)}";
            if (continueFrom != null)
                url += "&apcontinue=" + Uri.EscapeDataString(continueFrom);

            using var doc = await GetJsonAsync(url, ct);
            foreach (var page in doc.RootElement.GetProperty("query").GetProperty("allpages").EnumerateArray())
                titles.Add(page.GetProperty("title").GetString() ?? "");

            continueFrom = doc.RootElement.TryGetProperty("continue", out var cont)
                ? cont.GetProperty("apcontinue").GetString()
                : null;
        } while (continueFrom != null);
        return titles;
    }

    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken ct) {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(ct);
        if (body.TrimStart().StartsWith('<'))
            throw new HttpRequestException("Wiki returned HTML instead of JSON (bot check?)");
        return JsonDocument.Parse(body);
    }
}
```

`ProgrammeSyncService.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartermaster.Data.NewsPosts;
using Quartermaster.Server.NewsPosts.Mistral;

namespace Quartermaster.Server.NewsPosts.Programme;

public class ProgrammeSyncService {
    private const int EmbedBatchSize = 32;

    private readonly ProgrammeRepository _repo;
    private readonly IWikiClient _wiki;
    private readonly IMistralClient _mistral;
    private readonly ProgrammeIndex _index;
    private readonly ILogger<ProgrammeSyncService> _logger;

    public ProgrammeSyncService(ProgrammeRepository repo, IWikiClient wiki, IMistralClient mistral, ProgrammeIndex index, ILogger<ProgrammeSyncService> logger) {
        _repo = repo;
        _wiki = wiki;
        _mistral = mistral;
        _index = index;
        _logger = logger;
    }

    public void LoadIndex() => _index.Reload(_repo.GetAllSections());

    public async Task SyncAllAsync(CancellationToken ct) {
        foreach (var source in _repo.GetSources()) {
            try {
                await SyncSourceAsync(source, ct);
            } catch (OperationCanceledException) when (ct.IsCancellationRequested) {
                throw;
            } catch (Exception ex) {
                _logger.LogWarning(ex, "Programme sync failed for {Source}", source.WikiPageTitle);
                _repo.MarkSourceFailed(source.Id, ex.Message);
            }
        }
        LoadIndex();
    }

    private async Task SyncSourceAsync(ProgrammeSource source, CancellationToken ct) {
        if (!source.IsPrefix) {
            var page = await _wiki.GetPageAsync(source.WikiPageTitle, ct);
            if (page.RevisionId != source.LastRevisionId)
                await SyncPageAsync(source.Id, page, ct);
            _repo.MarkSourceSynced(source.Id, page.RevisionId, DateTime.UtcNow);
            return;
        }

        var titles = await _wiki.ListSubpagesAsync(source.WikiPageTitle, ct);
        foreach (var title in titles) {
            var page = await _wiki.GetPageAsync(title, ct);
            await SyncPageAsync(source.Id, page, ct);
        }
        foreach (var stale in _repo.GetPageTitles(source.Id).Except(titles))
            _repo.DeletePage(source.Id, stale);
        _repo.MarkSourceSynced(source.Id, null, DateTime.UtcNow);
    }

    private async Task SyncPageAsync(Guid sourceId, WikiPage page, CancellationToken ct) {
        var parsed = WikitextSectionSplitter.Split(page.Title, page.Wikitext);
        var existing = _repo.GetSections(sourceId, page.Title).ToDictionary(s => s.HeadingPath);
        var now = DateTime.UtcNow;

        var added = new List<ProgrammeSection>();
        var changed = new List<ProgrammeSection>();
        var keptIds = new HashSet<Guid>();
        foreach (var section in parsed) {
            var hash = Hash(section.HeadingPath + "\n" + section.Body);
            if (existing.TryGetValue(section.HeadingPath, out var current)) {
                keptIds.Add(current.Id);
                if (current.ContentHash == hash)
                    continue;
                current.Body = section.Body;
                current.Anchor = section.Anchor;
                current.ContentHash = hash;
                current.UpdatedAt = now;
                changed.Add(current);
                continue;
            }
            added.Add(new ProgrammeSection {
                SourceId = sourceId,
                PageTitle = page.Title,
                Heading = section.Heading,
                HeadingPath = section.HeadingPath,
                Anchor = section.Anchor,
                Body = section.Body,
                ContentHash = hash,
                UpdatedAt = now
            });
        }

        var toEmbed = added.Concat(changed).ToList();
        foreach (var batch in toEmbed.Chunk(EmbedBatchSize)) {
            var vectors = await _mistral.EmbedAsync(batch.Select(s => s.HeadingPath + "\n" + s.Body).ToList(), ct);
            for (var i = 0; i < batch.Length; i++)
                batch[i].Embedding = EmbeddingCodec.Encode(vectors[i]);
        }

        var removedIds = existing.Values.Where(s => !keptIds.Contains(s.Id)).Select(s => s.Id).ToList();
        _repo.ApplyPageDiff(added, changed, removedIds);
    }

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
```

`ProgrammeSyncHostedService.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Linq;
using Microsoft.Extensions.Logging;
using Quartermaster.Data.NewsPosts;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.NewsPosts.Programme;

public class ProgrammeSyncHostedService : BackgroundService {
    private static readonly TimeSpan Interval = TimeSpan.FromDays(7);

    private readonly IServiceProvider _services;
    private readonly ILogger<ProgrammeSyncHostedService> _logger;

    public ProgrammeSyncHostedService(IServiceProvider services, ILogger<ProgrammeSyncHostedService> logger) {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        using (var scope = _services.CreateScope()) {
            scope.ServiceProvider.GetRequiredService<ProgrammeSyncService>().LoadIndex();
        }

        while (!stoppingToken.IsCancellationRequested) {
            try {
                await RunIfDueAsync(stoppingToken);
            } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                return;
            } catch (Exception ex) {
                _logger.LogError(ex, "Programme sync cycle failed");
            }

            try {
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            } catch (OperationCanceledException) {
                return;
            }
        }
    }

    /// <summary>Checks hourly and syncs when the oldest source is a week old, so a restart does not re-sync and a missed week catches up.</summary>
    private async Task RunIfDueAsync(CancellationToken ct) {
        using var scope = _services.CreateScope();
        var settings = NewsPostsSettings.ReadFrom(scope.ServiceProvider.GetRequiredService<OptionRepository>());
        if (!settings.Enabled || !settings.HasMistral)
            return;

        var repo = scope.ServiceProvider.GetRequiredService<ProgrammeRepository>();
        var sources = repo.GetSources();
        var oldest = sources.Count == 0 ? DateTime.UtcNow : sources.Min(s => s.LastSyncedAt ?? DateTime.MinValue);
        if (DateTime.UtcNow - oldest < Interval)
            return;

        await scope.ServiceProvider.GetRequiredService<ProgrammeSyncService>().SyncAllAsync(ct);
    }
}
```

`Program.cs` `ConfigureServices`, after the Forms block:

```csharp
        builder.Services.AddSingleton<NewsPosts.Programme.ProgrammeIndex>();
        builder.Services.AddScoped(sp => NewsPosts.NewsPostsSettings.ReadFrom(sp.GetRequiredService<OptionRepository>()));
        builder.Services.AddScoped<NewsPosts.Mistral.IMistralClient>(sp => new NewsPosts.Mistral.HttpMistralClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(),
            sp.GetRequiredService<NewsPosts.NewsPostsSettings>(),
            sp.GetRequiredService<ILogger<NewsPosts.Mistral.HttpMistralClient>>()));
        builder.Services.AddScoped<NewsPosts.Programme.IWikiClient>(sp => new NewsPosts.Programme.HttpWikiClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient()));
        builder.Services.AddScoped<NewsPosts.Programme.ProgrammeSyncService>();
        builder.Services.AddHostedService<NewsPosts.Programme.ProgrammeSyncHostedService>();
```

(The existing file uses namespace-relative names like `Forms.PdfTemplating.PdfTemplateEngine`; match that style. Add `using System.Net.Http;` and `using Microsoft.Extensions.Logging;` if missing.)

- [ ] **Step 4: Run tests** (`ProgrammeIndexTests`, `ProgrammeSyncServiceTests`, `WikitextSectionSplitterTests`): all pass.

- [ ] **Step 5: Checkpoint** (user commits)

---

### Task 6: Feed parsing and polling

**Files:**
- Create: `Quartermaster.Server/NewsPosts/Feeds/ParsedFeedItem.cs`, `HtmlText.cs`, `RssFeedParser.cs`, `TagesschauParser.cs`, `IFeedFetcher.cs`, `HttpFeedFetcher.cs`, `NewsFeedPoller.cs`
- Create: `Quartermaster.Server.Tests/TestData/NewsPosts/netzpolitik.xml`, `atom.xml`, `tagesschau-news.json`, `tagesschau-details.json`
- Create: `Quartermaster.Server.Tests/NewsPosts/Fakes/FakeFeedFetcher.cs`
- Modify: `Quartermaster.Server/Program.cs`
- Test: `Quartermaster.Server.Tests/NewsPosts/FeedParserTests.cs`, `NewsFeedPollerTests.cs`

**Interfaces:**
- Consumes: `NewsFeedRepository`, `NewsItemRepository` (Task 1)
- Produces:
  - `record ParsedFeedItem(string ExternalId, string Url, string Title, string Teaser, string? ArticleText, DateTime PublishedAt, string? DetailsUrl)`
  - `RssFeedParser.Parse(string xml, DateTime nowUtc) : List<ParsedFeedItem>` (RSS 2.0 and Atom)
  - `TagesschauParser.ParseList(string json, DateTime nowUtc) : List<ParsedFeedItem>`; `TagesschauParser.ParseDetails(string json) : string`
  - `HtmlText.ToPlain(string html) : string`
  - `IFeedFetcher.GetStringAsync(string url, CancellationToken ct) : Task<string>`
  - `NewsFeedPoller.PollAllAsync(TimeSpan maxItemAge, DateTime nowUtc, CancellationToken ct) : Task<int>` (returns number of new items)

- [ ] **Step 1: Create test data and write the failing tests**

`TestData/NewsPosts/netzpolitik.xml`:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<rss version="2.0" xmlns:content="http://purl.org/rss/1.0/modules/content/">
  <channel>
    <title>netzpolitik.org</title>
    <item>
      <title>Eckpunkte geleakt: EU-Kommission will Alterskontrollen</title>
      <link>https://netzpolitik.org/2026/eckpunkte/</link>
      <guid isPermaLink="false">https://netzpolitik.org/?p=1</guid>
      <pubDate>Tue, 15 Sep 2026 10:00:00 +0200</pubDate>
      <description><![CDATA[<p>Die Kommission plant eine Alterskontrolle.</p>]]></description>
      <content:encoded><![CDATA[<p>Ein <strong>elfseitiges</strong> Papier.</p><p>Zweiter Absatz.</p>]]></content:encoded>
    </item>
    <item>
      <title>Ohne GUID und Datum</title>
      <link>https://netzpolitik.org/2026/ohne/</link>
      <description>Nur Teaser</description>
    </item>
  </channel>
</rss>
```

`TestData/NewsPosts/atom.xml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<feed xmlns="http://www.w3.org/2005/Atom">
  <title>heise</title>
  <entry>
    <title>Atom Titel</title>
    <link href="https://example.org/atom-1"/>
    <id>tag:example.org,2026:1</id>
    <updated>2026-09-16T08:00:00Z</updated>
    <summary>Atom Teaser</summary>
  </entry>
</feed>
```

`TestData/NewsPosts/tagesschau-news.json` (trimmed real shape, two stories and a video):

```json
{
  "news": [
    { "externalId": "ts-1", "title": "Tickets teurer", "firstSentence": "Bus und Bahn kosten mehr.", "date": "2026-09-16T10:00:00.000+02:00",
      "details": "https://www.tagesschau.de/api2u/inland/tickets-100.json", "shareURL": "https://www.tagesschau.de/inland/tickets-100.html", "type": "story" },
    { "externalId": "ts-2", "title": "Ohne Teaser", "date": "2026-09-16T11:00:00.000+02:00",
      "details": "https://www.tagesschau.de/api2u/inland/ohne-100.json", "shareURL": "https://www.tagesschau.de/inland/ohne-100.html", "type": "story" },
    { "externalId": "ts-3", "title": "Video", "date": "2026-09-16T12:00:00.000+02:00", "type": "video" }
  ],
  "regional": [],
  "type": "newsPage"
}
```

`TestData/NewsPosts/tagesschau-details.json`:

```json
{
  "content": [
    { "value": "<strong>Einzelfahrschein 4 Euro.</strong>", "type": "text" },
    { "value": "<h2>Zwischentitel</h2>", "type": "headline" },
    { "type": "image_gallery" },
    { "value": "Der Senat <a href=\"https://x\">verschiebt</a> die Reform.", "type": "text" }
  ]
}
```

`Fakes/FakeFeedFetcher.cs`:

```csharp
using Quartermaster.Server.NewsPosts.Feeds;

namespace Quartermaster.Server.Tests.NewsPosts.Fakes;

public class FakeFeedFetcher : IFeedFetcher {
    public Dictionary<string, string> Responses { get; } = [];

    public Task<string> GetStringAsync(string url, CancellationToken ct) {
        if (!Responses.TryGetValue(url, out var body))
            throw new HttpRequestException("404 " + url);
        return Task.FromResult(body);
    }

    public static string Load(string file) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "NewsPosts", file));
}
```

`FeedParserTests.cs`:

```csharp
using Quartermaster.Server.NewsPosts.Feeds;
using Quartermaster.Server.Tests.NewsPosts.Fakes;

namespace Quartermaster.Server.Tests.NewsPosts;

public class FeedParserTests {
    private static readonly DateTime Now = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task Rss_uses_content_encoded_as_plain_article_text() {
        var items = RssFeedParser.Parse(FakeFeedFetcher.Load("netzpolitik.xml"), Now);

        await Assert.That(items[0].ExternalId).IsEqualTo("https://netzpolitik.org/?p=1");
        await Assert.That(items[0].Teaser).IsEqualTo("Die Kommission plant eine Alterskontrolle.");
        await Assert.That(items[0].ArticleText).IsEqualTo("Ein elfseitiges Papier.\n\nZweiter Absatz.");
        await Assert.That(items[0].PublishedAt).IsEqualTo(new DateTime(2026, 9, 15, 8, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public async Task Rss_item_without_guid_and_date_falls_back_to_link_and_now() {
        var items = RssFeedParser.Parse(FakeFeedFetcher.Load("netzpolitik.xml"), Now);

        await Assert.That(items[1].ExternalId).IsEqualTo("https://netzpolitik.org/2026/ohne/");
        await Assert.That(items[1].PublishedAt).IsEqualTo(Now);
        await Assert.That(items[1].ArticleText).IsNull();
    }

    [Test]
    public async Task Atom_entries_are_parsed() {
        var items = RssFeedParser.Parse(FakeFeedFetcher.Load("atom.xml"), Now);

        await Assert.That(items.Single().Url).IsEqualTo("https://example.org/atom-1");
        await Assert.That(items.Single().Teaser).IsEqualTo("Atom Teaser");
    }

    [Test]
    public async Task Tagesschau_list_keeps_stories_only() {
        var items = TagesschauParser.ParseList(FakeFeedFetcher.Load("tagesschau-news.json"), Now);

        await Assert.That(items.Select(i => i.ExternalId).ToList()).IsEquivalentTo(new List<string> { "ts-1", "ts-2" });
        await Assert.That(items[0].Url).IsEqualTo("https://www.tagesschau.de/inland/tickets-100.html");
        await Assert.That(items[0].DetailsUrl).IsEqualTo("https://www.tagesschau.de/api2u/inland/tickets-100.json");
        await Assert.That(items[1].Teaser).IsEqualTo("");
    }

    [Test]
    public async Task Tagesschau_details_joins_text_and_headlines_as_plain_text() {
        var text = TagesschauParser.ParseDetails(FakeFeedFetcher.Load("tagesschau-details.json"));

        await Assert.That(text).IsEqualTo("Einzelfahrschein 4 Euro.\n\nZwischentitel\n\nDer Senat verschiebt die Reform.");
    }

    [Test]
    public async Task Malformed_xml_throws_FormatException() {
        await Assert.ThrowsAsync<FormatException>(() => Task.FromResult(RssFeedParser.Parse("<html>nope", Now)));
    }
}
```

`NewsFeedPollerTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run to verify failure** (build errors)

- [ ] **Step 3: Implement**

`ParsedFeedItem.cs`:

```csharp
using System;

namespace Quartermaster.Server.NewsPosts.Feeds;

public record ParsedFeedItem(string ExternalId, string Url, string Title, string Teaser, string? ArticleText, DateTime PublishedAt, string? DetailsUrl);
```

`HtmlText.cs`:

```csharp
using System.Net;
using System.Text.RegularExpressions;

namespace Quartermaster.Server.NewsPosts.Feeds;

public static partial class HtmlText {
    /// <summary>Turns article HTML into plain text: block ends become paragraph breaks, tags are dropped, entities decoded.</summary>
    public static string ToPlain(string html) {
        var text = BlockEndRegex().Replace(html, "\n\n");
        text = BreakRegex().Replace(text, "\n");
        text = TagRegex().Replace(text, "");
        text = WebUtility.HtmlDecode(text);
        text = InlineWhitespaceRegex().Replace(text, " ");
        text = SpaceAroundNewlineRegex().Replace(text, "\n");
        text = ManyNewlinesRegex().Replace(text, "\n\n");
        return text.Trim();
    }

    [GeneratedRegex(@"</(p|h[1-6]|li|div|blockquote)>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockEndRegex();

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BreakRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"[ \t\r\f\v]+")]
    private static partial Regex InlineWhitespaceRegex();

    [GeneratedRegex(@" *\n *")]
    private static partial Regex SpaceAroundNewlineRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ManyNewlinesRegex();
}
```

`RssFeedParser.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace Quartermaster.Server.NewsPosts.Feeds;

public static class RssFeedParser {
    private static readonly XNamespace Content = "http://purl.org/rss/1.0/modules/content/";
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";

    public static List<ParsedFeedItem> Parse(string xml, DateTime nowUtc) {
        XDocument doc;
        try {
            doc = XDocument.Parse(xml);
        } catch (XmlException ex) {
            throw new FormatException("Feed is not valid XML: " + ex.Message, ex);
        }

        var atomEntries = doc.Descendants(Atom + "entry").ToList();
        if (atomEntries.Count > 0)
            return atomEntries.Select(e => ParseAtomEntry(e, nowUtc)).Where(i => i.Url.Length > 0).ToList();

        return doc.Descendants("item").Select(e => ParseRssItem(e, nowUtc)).Where(i => i.Url.Length > 0).ToList();
    }

    private static ParsedFeedItem ParseRssItem(XElement item, DateTime nowUtc) {
        var link = item.Element("link")?.Value.Trim() ?? "";
        var guid = item.Element("guid")?.Value.Trim();
        var encoded = item.Element(Content + "encoded")?.Value;
        return new ParsedFeedItem(
            string.IsNullOrEmpty(guid) ? link : guid,
            link,
            HtmlText.ToPlain(item.Element("title")?.Value ?? ""),
            HtmlText.ToPlain(item.Element("description")?.Value ?? ""),
            string.IsNullOrWhiteSpace(encoded) ? null : HtmlText.ToPlain(encoded),
            ParseDate(item.Element("pubDate")?.Value, nowUtc),
            null);
    }

    private static ParsedFeedItem ParseAtomEntry(XElement entry, DateTime nowUtc) {
        var link = entry.Elements(Atom + "link")
            .Where(l => (string?)l.Attribute("rel") == null || (string?)l.Attribute("rel") == "alternate")
            .Select(l => (string?)l.Attribute("href"))
            .FirstOrDefault() ?? "";
        var id = entry.Element(Atom + "id")?.Value.Trim();
        var content = entry.Element(Atom + "content")?.Value;
        return new ParsedFeedItem(
            string.IsNullOrEmpty(id) ? link : id,
            link,
            HtmlText.ToPlain(entry.Element(Atom + "title")?.Value ?? ""),
            HtmlText.ToPlain(entry.Element(Atom + "summary")?.Value ?? ""),
            string.IsNullOrWhiteSpace(content) ? null : HtmlText.ToPlain(content),
            ParseDate(entry.Element(Atom + "updated")?.Value ?? entry.Element(Atom + "published")?.Value, nowUtc),
            null);
    }

    private static DateTime ParseDate(string? raw, DateTime nowUtc) {
        if (string.IsNullOrWhiteSpace(raw))
            return nowUtc;
        if (DateTimeOffset.TryParse(raw.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            return parsed.UtcDateTime;
        return nowUtc;
    }
}
```

`TagesschauParser.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace Quartermaster.Server.NewsPosts.Feeds;

public static class TagesschauParser {
    public static List<ParsedFeedItem> ParseList(string json, DateTime nowUtc) {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("news")
            .EnumerateArray()
            .Where(n => Str(n, "type") == "story")
            .Select(n => new ParsedFeedItem(
                Str(n, "externalId") ?? Str(n, "shareURL") ?? "",
                Str(n, "shareURL") ?? "",
                Str(n, "title") ?? "",
                Str(n, "firstSentence") ?? "",
                null,
                DateTimeOffset.TryParse(Str(n, "date"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date) ? date.UtcDateTime : nowUtc,
                Str(n, "details")))
            .Where(i => i.Url.Length > 0)
            .ToList();
    }

    public static string ParseDetails(string json) {
        using var doc = JsonDocument.Parse(json);
        var parts = doc.RootElement.GetProperty("content")
            .EnumerateArray()
            .Where(c => Str(c, "type") == "text" || Str(c, "type") == "headline")
            .Select(c => HtmlText.ToPlain(Str(c, "value") ?? ""))
            .Where(t => t.Length > 0);
        return string.Join("\n\n", parts);
    }

    private static string? Str(JsonElement element, string name) {
        if (!element.TryGetProperty(name, out var value))
            return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }
}
```

`IFeedFetcher.cs`:

```csharp
using System.Threading;
using System.Threading.Tasks;

namespace Quartermaster.Server.NewsPosts.Feeds;

public interface IFeedFetcher {
    Task<string> GetStringAsync(string url, CancellationToken ct);
}
```

`HttpFeedFetcher.cs`:

```csharp
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Quartermaster.Server.NewsPosts.Feeds;

public class HttpFeedFetcher : IFeedFetcher {
    private const string UserAgent = "Quartermaster/3 (+https://piratenpartei.de)";

    private readonly HttpClient _http;

    public HttpFeedFetcher(HttpClient http) {
        _http = http;
    }

    public async Task<string> GetStringAsync(string url, CancellationToken ct) {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }
}
```

`NewsFeedPoller.cs`:

```csharp
using System;
using System.Collections.Generic;
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
        List<ParsedFeedItem> items = feed.Kind == NewsFeedKind.TagesschauApi
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
        } catch (Exception ex) {
            _logger.LogInformation(ex, "Details for {Url} unavailable, using teaser", parsed.Url);
            return null;
        }
    }

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];
}
```

`NewsItemRepository` gains `public bool Exists(Guid feedId, string externalId) => _context.NewsItems.Any(i => i.FeedId == feedId && i.ExternalId == externalId);`, and `TryInsert` uses it, so detail pages are only fetched for new items.

`Program.cs`:

```csharp
        builder.Services.AddScoped<NewsPosts.Feeds.IFeedFetcher>(sp => new NewsPosts.Feeds.HttpFeedFetcher(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient()));
        builder.Services.AddScoped<NewsPosts.Feeds.NewsFeedPoller>();
```

- [ ] **Step 4: Run tests** (`FeedParserTests`, `NewsFeedPollerTests`): all pass.

- [ ] **Step 5: Checkpoint** (user commits)

---

### Task 7: Triage and candidate generation

**Files:**
- Create: `Quartermaster.Server/NewsPosts/Generation/LlmJson.cs`, `TriageResult.cs`, `RelevanceTriage.cs`, `GeneratedCandidate.cs`, `CandidateValidator.cs`, `CandidateGenerator.cs`
- Test: `Quartermaster.Server.Tests/NewsPosts/RelevanceTriageTests.cs`, `CandidateGeneratorTests.cs`

**Interfaces:**
- Consumes: `IMistralClient` (Task 2), `NewsPostsSettings` (Task 2), `NewsItem`, `ProgrammeSection` (Task 1), `PostText` (Task 1)
- Produces:
  - `LlmJson.Parse(string raw) : JsonDocument` (tolerates a Markdown code fence; throws `MistralException` on invalid JSON)
  - `record TriageResult(bool Relevant, string Reason)`
  - `RelevanceTriage.RunAsync(NewsItem item, IReadOnlyList<ProgrammeSection> passages, CancellationToken ct) : Task<TriageResult>`
  - `record GeneratedCandidate(string Text, Guid SectionId)`
  - `CandidateValidator.Validate(IEnumerable<GeneratedCandidate> raw, IReadOnlyCollection<Guid> allowedSectionIds, int maxCount) : List<GeneratedCandidate>`
  - `CandidateGenerator.GenerateAsync(NewsItem item, IReadOnlyList<ProgrammeSection> passages, CancellationToken ct) : Task<List<GeneratedCandidate>>`

- [ ] **Step 1: Write the failing tests**

`RelevanceTriageTests.cs`:

```csharp
using Quartermaster.Data.NewsPosts;
using Quartermaster.Server.NewsPosts;
using Quartermaster.Server.NewsPosts.Generation;
using Quartermaster.Server.NewsPosts.Mistral;
using Quartermaster.Server.Tests.NewsPosts.Fakes;

namespace Quartermaster.Server.Tests.NewsPosts;

public class RelevanceTriageTests {
    private static readonly NewsItem Item = new() { Title = "Alterskontrolle für alle", Teaser = "EU plant Ausweispflicht" };
    private static readonly List<ProgrammeSection> Passages = [new ProgrammeSection { HeadingPath = "P > Jugendschutz", Body = "Aufklärung statt Verbote." }];

    [Test]
    public async Task Parses_verdict_and_sends_passages_to_triage_model() {
        var mistral = new FakeMistralClient();
        mistral.ChatAnswers.Enqueue("""{"relevant": true, "reason": "Betrifft Jugendschutz"}""");
        var triage = new RelevanceTriage(mistral, new NewsPostsSettings { TriageModel = "small" });

        var result = await triage.RunAsync(Item, Passages, CancellationToken.None);

        await Assert.That(result).IsEqualTo(new TriageResult(true, "Betrifft Jugendschutz"));
        await Assert.That(mistral.ChatCalls[0].Model).IsEqualTo("small");
        await Assert.That(mistral.ChatCalls[0].User).Contains("Aufklärung statt Verbote.");
    }

    [Test]
    public async Task Fenced_json_is_accepted() {
        var mistral = new FakeMistralClient();
        mistral.ChatAnswers.Enqueue("```json\n{\"relevant\": false, \"reason\": \"Sport\"}\n```");
        var triage = new RelevanceTriage(mistral, new NewsPostsSettings());

        var result = await triage.RunAsync(Item, Passages, CancellationToken.None);

        await Assert.That(result.Relevant).IsFalse();
    }

    [Test]
    public async Task Non_json_answer_throws_MistralException() {
        var mistral = new FakeMistralClient();
        mistral.ChatAnswers.Enqueue("Ja, das ist relevant.");
        var triage = new RelevanceTriage(mistral, new NewsPostsSettings());

        await Assert.ThrowsAsync<MistralException>(() => triage.RunAsync(Item, Passages, CancellationToken.None));
    }
}
```

`CandidateGeneratorTests.cs`:

```csharp
using Quartermaster.Data.NewsPosts;
using Quartermaster.Server.NewsPosts;
using Quartermaster.Server.NewsPosts.Generation;
using Quartermaster.Server.NewsPosts.Mistral;
using Quartermaster.Server.Tests.NewsPosts.Fakes;

namespace Quartermaster.Server.Tests.NewsPosts;

public class CandidateGeneratorTests {
    private static readonly ProgrammeSection Section = new() { HeadingPath = "P > Verkehr", Body = "ÖPNV fahrscheinfrei." };
    private static readonly NewsItem Item = new() { Title = "Tickets teurer", Teaser = "4 Euro", ArticleText = "Einzelfahrschein 4 Euro." };

    private static NewsPostsSettings Settings() => new() { GenerationModel = "medium", CandidateCount = 3, GenerationPrompt = "Sei pointiert." };

    [Test]
    public async Task Returns_valid_candidates_and_sends_rules_and_article() {
        var mistral = new FakeMistralClient();
        mistral.ChatAnswers.Enqueue($$"""{"candidates":[{"text":"Bus fahrscheinfrei!","sectionId":"{{Section.Id}}"}]}""");
        var generator = new CandidateGenerator(mistral, Settings());

        var result = await generator.GenerateAsync(Item, [Section], CancellationToken.None);

        await Assert.That(result.Single()).IsEqualTo(new GeneratedCandidate("Bus fahrscheinfrei!", Section.Id));
        await Assert.That(mistral.ChatCalls[0].Model).IsEqualTo("medium");
        await Assert.That(mistral.ChatCalls[0].System).Contains("Sei pointiert.");
        await Assert.That(mistral.ChatCalls[0].User).Contains("Einzelfahrschein 4 Euro.");
        await Assert.That(mistral.ChatCalls[0].User).Contains(Section.Id.ToString());
    }

    [Test]
    public async Task Empty_candidate_list_is_valid() {
        var mistral = new FakeMistralClient();
        mistral.ChatAnswers.Enqueue("""{"candidates":[]}""");
        var generator = new CandidateGenerator(mistral, Settings());

        await Assert.That(await generator.GenerateAsync(Item, [Section], CancellationToken.None)).IsEmpty();
    }

    [Test]
    public async Task Validator_drops_too_long_unknown_section_blank_and_extra_candidates() {
        var other = Guid.NewGuid();
        var raw = new List<GeneratedCandidate> {
            new("ok 1", Section.Id),
            new(new string('x', 281), Section.Id),
            new("fremde Stelle", other),
            new("   ", Section.Id),
            new("ok 2", Section.Id),
            new("ok 3", Section.Id),
            new("ok 4", Section.Id)
        };

        var valid = CandidateValidator.Validate(raw, [Section.Id], 3);

        await Assert.That(valid.Select(c => c.Text).ToList()).IsEquivalentTo(new List<string> { "ok 1", "ok 2", "ok 3" });
    }

    [Test]
    public async Task Missing_candidates_property_throws_MistralException() {
        var mistral = new FakeMistralClient();
        mistral.ChatAnswers.Enqueue("""{"posts":[]}""");
        var generator = new CandidateGenerator(mistral, Settings());

        await Assert.ThrowsAsync<MistralException>(() => generator.GenerateAsync(Item, [Section], CancellationToken.None));
    }
}
```

- [ ] **Step 2: Run to verify failure** (build errors)

- [ ] **Step 3: Implement**

`LlmJson.cs`:

```csharp
using System.Text.Json;
using System.Text.RegularExpressions;
using Quartermaster.Server.NewsPosts.Mistral;

namespace Quartermaster.Server.NewsPosts.Generation;

public static partial class LlmJson {
    /// <summary>JSON mode normally returns bare JSON, but models occasionally wrap it in a Markdown fence; accept both.</summary>
    public static JsonDocument Parse(string raw) {
        var match = FenceRegex().Match(raw);
        var json = match.Success ? match.Groups[1].Value : raw;
        try {
            return JsonDocument.Parse(json);
        } catch (JsonException ex) {
            throw new MistralException("Model answer is not valid JSON: " + ex.Message);
        }
    }

    [GeneratedRegex(@"```(?:json)?\s*(.*?)\s*```", RegexOptions.Singleline)]
    private static partial Regex FenceRegex();
}
```

`TriageResult.cs`:

```csharp
namespace Quartermaster.Server.NewsPosts.Generation;

public record TriageResult(bool Relevant, string Reason);
```

`RelevanceTriage.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Quartermaster.Data.NewsPosts;
using Quartermaster.Server.NewsPosts.Mistral;

namespace Quartermaster.Server.NewsPosts.Generation;

public class RelevanceTriage {
    private const string SystemPrompt =
        "Du prüfst, ob eine Nachrichtenmeldung einen klaren Bezug zu den angegebenen Programmstellen der Piratenpartei hat, " +
        "sodass die Partei sich glaubwürdig dazu äußern kann. Antworte ausschließlich als JSON: " +
        "{\"relevant\": true|false, \"reason\": \"ein kurzer Satz auf Deutsch\"}.";

    private readonly IMistralClient _mistral;
    private readonly NewsPostsSettings _settings;

    public RelevanceTriage(IMistralClient mistral, NewsPostsSettings settings) {
        _mistral = mistral;
        _settings = settings;
    }

    public async Task<TriageResult> RunAsync(NewsItem item, IReadOnlyList<ProgrammeSection> passages, CancellationToken ct) {
        var user = new StringBuilder();
        user.AppendLine("Meldung: " + item.Title);
        user.AppendLine("Teaser: " + item.Teaser);
        user.AppendLine();
        user.AppendLine("Programmstellen:");
        foreach (var passage in passages)
            user.AppendLine($"- {passage.HeadingPath}: {Shorten(passage.Body, 600)}");

        var raw = await _mistral.CompleteJsonAsync(_settings.TriageModel, SystemPrompt, user.ToString(), ct);
        using var doc = LlmJson.Parse(raw);
        if (!doc.RootElement.TryGetProperty("relevant", out var relevant) || (relevant.ValueKind != JsonValueKind.True && relevant.ValueKind != JsonValueKind.False))
            throw new MistralException("Triage answer lacks a boolean 'relevant'");

        var reason = doc.RootElement.TryGetProperty("reason", out var r) ? r.GetString() ?? "" : "";
        return new TriageResult(relevant.GetBoolean(), reason.Trim());
    }

    private static string Shorten(string text, int max) => text.Length <= max ? text : text[..max] + "...";
}
```

(The `relevant` check spans one line with two conditions on the value kind; if it reads long, extract `private static bool IsBoolean(JsonElement e)` per `CLAUDE.md`'s complex-conditional rule.)

`GeneratedCandidate.cs`:

```csharp
using System;

namespace Quartermaster.Server.NewsPosts.Generation;

public record GeneratedCandidate(string Text, Guid SectionId);
```

`CandidateValidator.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Quartermaster.Api.NewsPosts;

namespace Quartermaster.Server.NewsPosts.Generation;

public static class CandidateValidator {
    public static List<GeneratedCandidate> Validate(IEnumerable<GeneratedCandidate> raw, IReadOnlyCollection<Guid> allowedSectionIds, int maxCount) {
        return raw
            .Select(c => c with { Text = c.Text.Trim() })
            .Where(c => PostText.IsValid(c.Text))
            .Where(c => allowedSectionIds.Contains(c.SectionId))
            .Take(maxCount)
            .ToList();
    }
}
```

`CandidateGenerator.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Quartermaster.Api.NewsPosts;
using Quartermaster.Data.NewsPosts;
using Quartermaster.Server.NewsPosts.Mistral;

namespace Quartermaster.Server.NewsPosts.Generation;

public class CandidateGenerator {
    private readonly IMistralClient _mistral;
    private readonly NewsPostsSettings _settings;

    public CandidateGenerator(IMistralClient mistral, NewsPostsSettings settings) {
        _mistral = mistral;
        _settings = settings;
    }

    public async Task<List<GeneratedCandidate>> GenerateAsync(NewsItem item, IReadOnlyList<ProgrammeSection> passages, CancellationToken ct) {
        var raw = await _mistral.CompleteJsonAsync(_settings.GenerationModel, BuildSystemPrompt(), BuildUserPrompt(item, passages), ct);
        using var doc = LlmJson.Parse(raw);
        if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array)
            throw new MistralException("Generation answer lacks a 'candidates' array");

        var parsed = candidates.EnumerateArray()
            .Select(ParseCandidate)
            .Where(c => c != null)
            .Select(c => c!)
            .ToList();
        return CandidateValidator.Validate(parsed, passages.Select(p => p.Id).ToList(), _settings.CandidateCount);
    }

    private string BuildSystemPrompt() {
        return _settings.GenerationPrompt + "\n\n" +
            $"Schreibe bis zu {_settings.CandidateCount} verschiedene Beiträge auf Deutsch zu der Meldung, jeder höchstens {PostText.MaxLength} Zeichen lang. " +
            "Zahlen, Namen und Zitate dürfen nur aus dem Artikeltext stammen, nichts erfinden. " +
            "Jeder Beitrag stützt sich auf genau eine der angegebenen Programmstellen und nennt deren Id. " +
            "Wenn keine Programmstelle wirklich passt, gib eine leere Liste zurück. " +
            "Antworte ausschließlich als JSON: {\"candidates\": [{\"text\": \"...\", \"sectionId\": \"<Id>\"}]}.";
    }

    private static string BuildUserPrompt(NewsItem item, IReadOnlyList<ProgrammeSection> passages) {
        var user = new StringBuilder();
        user.AppendLine("Meldung: " + item.Title);
        user.AppendLine("Quelle: " + item.Url);
        user.AppendLine();
        user.AppendLine("Artikeltext:");
        user.AppendLine(string.IsNullOrWhiteSpace(item.ArticleText) ? item.Teaser : item.ArticleText);
        user.AppendLine();
        user.AppendLine("Programmstellen:");
        foreach (var passage in passages) {
            user.AppendLine($"[Id {passage.Id}] {passage.HeadingPath}");
            user.AppendLine(passage.Body);
            user.AppendLine();
        }
        return user.ToString();
    }

    private static GeneratedCandidate? ParseCandidate(JsonElement element) {
        var text = element.TryGetProperty("text", out var t) ? t.GetString() : null;
        var sectionRaw = element.TryGetProperty("sectionId", out var s) ? s.GetString() : null;
        if (text == null || !Guid.TryParse(sectionRaw, out var sectionId))
            return null;
        return new GeneratedCandidate(text, sectionId);
    }
}
```

- [ ] **Step 4: Run tests** (`RelevanceTriageTests`, `CandidateGeneratorTests`): all pass.

- [ ] **Step 5: Checkpoint** (user commits)

---

### Task 8: Pipeline orchestration and hosted service

**Files:**
- Create: `Quartermaster.Server/NewsPosts/Pipeline/NewsPipeline.cs`, `NewsPipelineHostedService.cs`
- Modify: `Quartermaster.Server/Program.cs`
- Test: `Quartermaster.Server.Tests/NewsPosts/NewsPipelineTests.cs`

**Interfaces:**
- Consumes: everything from Tasks 1 to 7.
- Produces:
  - `NewsPipeline.RunOnceAsync(DateTime nowUtc, CancellationToken ct) : Task` (poll, match, triage, generate, retention)
  - `NewsPipeline.GenerateForItemAsync(Guid itemId, CancellationToken ct) : Task<bool>` (manual generate; false when the item does not exist or is `Posted`/`Posting`)
  - `NewsPipeline.ResetFailed(Guid itemId) : bool`
  - `NewsPipeline.MaxFailures` = 3
  - Static status snapshot for the settings page: `NewsPipelineHostedService.LastRunAt` (`DateTime?`) and `LastRunError` (`string?`)

- [ ] **Step 1: Write the failing tests**

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using Quartermaster.Data.NewsPosts;
using Quartermaster.Server.NewsPosts;
using Quartermaster.Server.NewsPosts.Feeds;
using Quartermaster.Server.NewsPosts.Generation;
using Quartermaster.Server.NewsPosts.Pipeline;
using Quartermaster.Server.NewsPosts.Programme;
using Quartermaster.Server.Tests.Infrastructure;
using Quartermaster.Server.Tests.NewsPosts.Fakes;

namespace Quartermaster.Server.Tests.NewsPosts;

public class NewsPipelineTests : RepositoryTestBase {
    private static readonly DateTime Now = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);

    private NewsItemRepository _itemRepo = default!;
    private ProgrammeRepository _programmeRepo = default!;
    private FakeMistralClient _mistral = default!;
    private ProgrammeIndex _index = default!;
    private NewsPipeline _pipeline = default!;
    private NewsFeed _feed = default!;
    private ProgrammeSection _youthSection = default!;

    [Before(Test)]
    public void Setup() {
        var feedRepo = new NewsFeedRepository(Db);
        _itemRepo = new NewsItemRepository(Db);
        _programmeRepo = new ProgrammeRepository(Db);
        _mistral = new FakeMistralClient();
        _index = new ProgrammeIndex();
        var settings = new NewsPostsSettings { MinSimilarity = 0.7, CandidateCount = 3, Retention = TimeSpan.FromDays(30) };

        _feed = new NewsFeed { Name = "f", Url = "https://f", Kind = NewsFeedKind.Rss, IsEnabled = false };
        feedRepo.Create(_feed);

        var source = new ProgrammeSource { WikiPageTitle = "P" };
        _programmeRepo.CreateSource(source);
        _youthSection = new ProgrammeSection {
            SourceId = source.Id, PageTitle = "P", Heading = "Jugendschutz", HeadingPath = "P > Jugendschutz",
            Anchor = "Jugendschutz", Body = "Jugendschutz durch Aufklärung.", ContentHash = "h",
            Embedding = EmbeddingCodec.Encode(FakeMistralClient.Vector("Jugendschutz")), UpdatedAt = Now
        };
        _programmeRepo.ApplyPageDiff([_youthSection], [], []);
        _index.Reload(_programmeRepo.GetAllSections());

        var poller = new NewsFeedPoller(feedRepo, _itemRepo, new FakeFeedFetcher(), NullLogger<NewsFeedPoller>.Instance);
        _pipeline = new NewsPipeline(poller, _itemRepo, _programmeRepo, _index, _mistral,
            new RelevanceTriage(_mistral, settings), new CandidateGenerator(_mistral, settings), settings,
            NullLogger<NewsPipeline>.Instance);
    }

    private NewsItem Seed(string title, NewsItemStatus status = NewsItemStatus.New) {
        var item = new NewsItem {
            FeedId = _feed.Id, ExternalId = title, Url = "https://x/" + title, Title = title, Teaser = "",
            PublishedAt = Now, FetchedAt = Now, UpdatedAt = Now, Status = status
        };
        _itemRepo.TryInsert(item);
        return item;
    }

    [Test]
    public async Task Low_similarity_item_becomes_irrelevant_without_llm_call() {
        var item = Seed("Fußball-Ergebnisse");

        await _pipeline.RunOnceAsync(Now, CancellationToken.None);

        await Assert.That(_itemRepo.Get(item.Id)!.Status).IsEqualTo(NewsItemStatus.Irrelevant);
        await Assert.That(_mistral.ChatCalls).IsEmpty();
    }

    [Test]
    public async Task Relevant_item_is_triaged_and_generated_in_one_run() {
        var item = Seed("Alterskontrolle für Social Media");
        _mistral.ChatAnswers.Enqueue("""{"relevant": true, "reason": "Jugendschutz"}""");
        _mistral.ChatAnswers.Enqueue($$"""{"candidates":[{"text":"Nein zur Ausweispflicht.","sectionId":"{{_youthSection.Id}}"}]}""");

        await _pipeline.RunOnceAsync(Now, CancellationToken.None);

        var stored = _itemRepo.Get(item.Id)!;
        await Assert.That(stored.Status).IsEqualTo(NewsItemStatus.Generated);
        await Assert.That(stored.TriageReason).IsEqualTo("Jugendschutz");
        await Assert.That(_itemRepo.GetCandidates(item.Id).Single().SectionId).IsEqualTo(_youthSection.Id);
    }

    [Test]
    public async Task Bad_model_answer_counts_failure_and_third_failure_marks_failed() {
        var item = Seed("Alterskontrolle kommt");
        for (var i = 0; i < 3; i++)
            _mistral.ChatAnswers.Enqueue("kein json");

        await _pipeline.RunOnceAsync(Now, CancellationToken.None);
        await Assert.That(_itemRepo.Get(item.Id)!.FailureCount).IsEqualTo(1);
        await Assert.That(_itemRepo.Get(item.Id)!.Status).IsEqualTo(NewsItemStatus.New);

        await _pipeline.RunOnceAsync(Now, CancellationToken.None);
        await _pipeline.RunOnceAsync(Now, CancellationToken.None);

        var stored = _itemRepo.Get(item.Id)!;
        await Assert.That(stored.Status).IsEqualTo(NewsItemStatus.Failed);
        await Assert.That(stored.LastError).Contains("JSON");
    }

    [Test]
    public async Task One_failing_item_does_not_stop_the_next() {
        var first = Seed("Alterskontrolle A");
        var second = Seed("Alterskontrolle B");
        _mistral.ChatAnswers.Enqueue("kein json");
        _mistral.ChatAnswers.Enqueue("""{"relevant": false, "reason": "nein"}""");

        await _pipeline.RunOnceAsync(Now, CancellationToken.None);

        await Assert.That(_itemRepo.Get(first.Id)!.FailureCount).IsEqualTo(1);
        await Assert.That(_itemRepo.Get(second.Id)!.Status).IsEqualTo(NewsItemStatus.Irrelevant);
    }

    [Test]
    public async Task Manual_generate_works_for_irrelevant_item_and_refuses_posted() {
        var irrelevant = Seed("Alterskontrolle manuell", NewsItemStatus.Irrelevant);
        var posted = Seed("Alterskontrolle gepostet", NewsItemStatus.Posted);
        _mistral.ChatAnswers.Enqueue($$"""{"candidates":[{"text":"Text","sectionId":"{{_youthSection.Id}}"}]}""");

        await Assert.That(await _pipeline.GenerateForItemAsync(irrelevant.Id, CancellationToken.None)).IsTrue();
        await Assert.That(await _pipeline.GenerateForItemAsync(posted.Id, CancellationToken.None)).IsFalse();
        await Assert.That(_itemRepo.Get(irrelevant.Id)!.Status).IsEqualTo(NewsItemStatus.Generated);
    }

    [Test]
    public async Task ResetFailed_returns_item_to_new() {
        var item = Seed("x", NewsItemStatus.Failed);

        await Assert.That(_pipeline.ResetFailed(item.Id)).IsTrue();

        var stored = _itemRepo.Get(item.Id)!;
        await Assert.That(stored.Status).IsEqualTo(NewsItemStatus.New);
        await Assert.That(stored.FailureCount).IsEqualTo(0);
    }
}
```

- [ ] **Step 2: Run to verify failure** (build errors)

- [ ] **Step 3: Implement**

`Pipeline/NewsPipeline.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartermaster.Data.NewsPosts;
using Quartermaster.Server.NewsPosts.Feeds;
using Quartermaster.Server.NewsPosts.Generation;
using Quartermaster.Server.NewsPosts.Mistral;
using Quartermaster.Server.NewsPosts.Programme;

namespace Quartermaster.Server.NewsPosts.Pipeline;

public class NewsPipeline {
    public const int MaxFailures = 3;
    private const int MatchCount = 8;

    private readonly NewsFeedPoller _poller;
    private readonly NewsItemRepository _itemRepo;
    private readonly ProgrammeRepository _programmeRepo;
    private readonly ProgrammeIndex _index;
    private readonly IMistralClient _mistral;
    private readonly RelevanceTriage _triage;
    private readonly CandidateGenerator _generator;
    private readonly NewsPostsSettings _settings;
    private readonly ILogger<NewsPipeline> _logger;

    public NewsPipeline(NewsFeedPoller poller, NewsItemRepository itemRepo, ProgrammeRepository programmeRepo, ProgrammeIndex index,
        IMistralClient mistral, RelevanceTriage triage, CandidateGenerator generator, NewsPostsSettings settings, ILogger<NewsPipeline> logger) {
        _poller = poller;
        _itemRepo = itemRepo;
        _programmeRepo = programmeRepo;
        _index = index;
        _mistral = mistral;
        _triage = triage;
        _generator = generator;
        _settings = settings;
        _logger = logger;
    }

    public async Task RunOnceAsync(DateTime nowUtc, CancellationToken ct) {
        await _poller.PollAllAsync(_settings.MaxItemAge, nowUtc, ct);

        var fresh = _itemRepo.GetByStatus(NewsItemStatus.New);
        if (fresh.Count > 0 && _index.Count > 0)
            await MatchAndTriageAsync(fresh, ct);

        foreach (var item in _itemRepo.GetByStatus(NewsItemStatus.Relevant))
            await RunStageAsync(item, () => GenerateAsync(item, ct));

        var deleted = _itemRepo.DeleteStale(nowUtc - _settings.Retention);
        if (deleted > 0)
            _logger.LogInformation("News pipeline retention removed {Count} items", deleted);
    }

    public async Task<bool> GenerateForItemAsync(Guid itemId, CancellationToken ct) {
        var item = _itemRepo.Get(itemId);
        if (item == null || item.Status == NewsItemStatus.Posted || item.Status == NewsItemStatus.Posting)
            return false;

        if (ParseIds(item.MatchedSectionIds).Count == 0) {
            var vector = (await _mistral.EmbedAsync([EmbeddingInput(item)], ct))[0];
            ApplyMatches(item, _index.TopMatches(vector, MatchCount));
        }
        await GenerateAsync(item, ct);
        return true;
    }

    public bool ResetFailed(Guid itemId) {
        var item = _itemRepo.Get(itemId);
        if (item == null || item.Status != NewsItemStatus.Failed)
            return false;
        item.Status = NewsItemStatus.New;
        item.FailureCount = 0;
        item.LastError = null;
        _itemRepo.Update(item);
        return true;
    }

    private async Task MatchAndTriageAsync(List<NewsItem> items, CancellationToken ct) {
        List<float[]> vectors;
        try {
            vectors = await _mistral.EmbedAsync(items.Select(EmbeddingInput).ToList(), ct);
        } catch (MistralException ex) {
            _logger.LogWarning(ex, "Embedding {Count} news items failed", items.Count);
            foreach (var item in items)
                RecordFailure(item, ex.Message);
            return;
        }

        for (var i = 0; i < items.Count; i++) {
            var item = items[i];
            ApplyMatches(item, _index.TopMatches(vectors[i], MatchCount));
            if ((item.BestSimilarity ?? 0) < _settings.MinSimilarity) {
                item.Status = NewsItemStatus.Irrelevant;
                item.TriageReason = null;
                _itemRepo.Update(item);
                continue;
            }
            await RunStageAsync(item, () => TriageAsync(item, ct));
        }
    }

    private async Task TriageAsync(NewsItem item, CancellationToken ct) {
        var result = await _triage.RunAsync(item, LoadPassages(item), ct);
        item.Status = result.Relevant ? NewsItemStatus.Relevant : NewsItemStatus.Irrelevant;
        item.TriageReason = result.Reason;
        item.FailureCount = 0;
        item.LastError = null;
        _itemRepo.Update(item);

        if (result.Relevant)
            await RunStageAsync(item, () => GenerateAsync(item, ct));
    }

    private async Task GenerateAsync(NewsItem item, CancellationToken ct) {
        var candidates = await _generator.GenerateAsync(item, LoadPassages(item), ct);
        _itemRepo.ReplaceCandidates(item.Id, candidates
            .Select(c => new PostCandidate { Text = c.Text, SectionId = c.SectionId })
            .ToList());
        item.Status = NewsItemStatus.Generated;
        item.FailureCount = 0;
        item.LastError = null;
        _itemRepo.Update(item);
    }

    /// <summary>Runs one stage for one item; a model or network error is recorded on the item instead of aborting the run.</summary>
    private async Task RunStageAsync(NewsItem item, Func<Task> stage) {
        try {
            await stage();
        } catch (MistralException ex) {
            _logger.LogWarning(ex, "News item {Id} stage failed", item.Id);
            RecordFailure(item, ex.Message);
        } catch (HttpRequestException ex) {
            _logger.LogWarning(ex, "News item {Id} stage failed", item.Id);
            RecordFailure(item, ex.Message);
        }
    }

    private void RecordFailure(NewsItem item, string error) {
        item.FailureCount++;
        item.LastError = error;
        if (item.FailureCount >= MaxFailures)
            item.Status = NewsItemStatus.Failed;
        _itemRepo.Update(item);
    }

    private void ApplyMatches(NewsItem item, List<SectionMatch> matches) {
        item.MatchedSectionIds = JsonSerializer.Serialize(matches.Select(m => m.SectionId));
        item.BestSimilarity = matches.Count == 0 ? 0 : matches[0].Similarity;
    }

    private List<ProgrammeSection> LoadPassages(NewsItem item) {
        var ids = ParseIds(item.MatchedSectionIds);
        var byId = _programmeRepo.GetSectionsByIds(ids).ToDictionary(s => s.Id);
        return ids.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
    }

    private static List<Guid> ParseIds(string json) => JsonSerializer.Deserialize<List<Guid>>(json) ?? [];

    private static string EmbeddingInput(NewsItem item) => item.Title + "\n" + item.Teaser;
}
```

`Pipeline/NewsPipelineHostedService.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.NewsPosts.Pipeline;

public class NewsPipelineHostedService : BackgroundService {
    private readonly IServiceProvider _services;
    private readonly ILogger<NewsPipelineHostedService> _logger;

    public static DateTime? LastRunAt { get; private set; }
    public static string? LastRunError { get; private set; }

    public NewsPipelineHostedService(IServiceProvider services, ILogger<NewsPipelineHostedService> logger) {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        try {
            await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);
        } catch (OperationCanceledException) {
            return;
        }

        while (!stoppingToken.IsCancellationRequested) {
            var interval = TimeSpan.FromMinutes(15);
            try {
                interval = await RunCycleAsync(stoppingToken);
            } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                return;
            } catch (Exception ex) {
                LastRunError = ex.Message;
                _logger.LogError(ex, "News pipeline cycle failed");
            }

            try {
                await Task.Delay(interval, stoppingToken);
            } catch (OperationCanceledException) {
                return;
            }
        }
    }

    private async Task<TimeSpan> RunCycleAsync(CancellationToken ct) {
        using var scope = _services.CreateScope();
        var settings = NewsPostsSettings.ReadFrom(scope.ServiceProvider.GetRequiredService<OptionRepository>());
        if (!settings.Enabled || !settings.HasMistral)
            return settings.PollInterval;

        await scope.ServiceProvider.GetRequiredService<NewsPipeline>().RunOnceAsync(DateTime.UtcNow, ct);
        LastRunAt = DateTime.UtcNow;
        LastRunError = null;
        return settings.PollInterval;
    }
}
```

`Program.cs`:

```csharp
        builder.Services.AddScoped<NewsPosts.Generation.RelevanceTriage>();
        builder.Services.AddScoped<NewsPosts.Generation.CandidateGenerator>();
        builder.Services.AddScoped<NewsPosts.Pipeline.NewsPipeline>();
        builder.Services.AddHostedService<NewsPosts.Pipeline.NewsPipelineHostedService>();
        builder.Services.AddScoped<NewsPosts.Postiz.IPostizClient>(sp => new NewsPosts.Postiz.HttpPostizClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(),
            sp.GetRequiredService<NewsPosts.NewsPostsSettings>()));
```

- [ ] **Step 4: Run tests** (`NewsPipelineTests`): all pass. Then start the local server with `newsposts.enabled` off and confirm the startup log has no errors from the two new hosted services.

- [ ] **Step 5: Checkpoint** (user commits)

---

### Task 9: API endpoints

**Files:**
- Create: `Quartermaster.Api/NewsPosts/NewsPostDTOs.cs`
- Create in `Quartermaster.Server/NewsPosts/Endpoints/`: `NewsItemListEndpoint.cs`, `NewsItemGenerateEndpoint.cs`, `NewsItemDismissEndpoint.cs`, `NewsItemRetryEndpoint.cs`, `NewsChannelListEndpoint.cs`, `NewsPublishEndpoint.cs`, `NewsPublicationListEndpoint.cs`, `NewsFeedListEndpoint.cs`, `NewsFeedSaveEndpoint.cs`, `NewsFeedDeleteEndpoint.cs`, `ProgrammeSourceListEndpoint.cs`, `ProgrammeSourceCreateEndpoint.cs`, `ProgrammeSourceDeleteEndpoint.cs`, `ProgrammeSyncEndpoint.cs`, `NewsPostsStatusEndpoint.cs`, `NewsItemMapper.cs`
- Create: `Quartermaster.Server.Tests/NewsPosts/Fakes/FakePostizClient.cs`
- Test: `Quartermaster.Server.Tests/NewsPosts/PublishEndpointTests.cs`, `NewsPostsEndpointPermissionTests.cs`

**Interfaces:**
- Consumes: repositories (Task 1), `NewsPostsSettings` (Task 2), `IPostizClient`, `PostizChannel` (Task 3), `ProgrammeSyncService` (Task 5), `NewsPipeline`, `NewsPipelineHostedService` (Task 8), `PostText` (Task 1), `PermissionContext`, `AuditLogRepository`.
- Produces the HTTP API the Blazor pages use (routes below) and the DTOs in `Quartermaster.Api.NewsPosts`.

Routes:

```
GET    /api/newsposts/items?view=suggestions|all|posted&status=<int?>   newsposts_review   List<NewsItemDTO>
POST   /api/newsposts/items/{Id}/generate                               newsposts_review   204 | 404 | 409
POST   /api/newsposts/items/{Id}/dismiss                                newsposts_review   204 | 404
POST   /api/newsposts/items/{Id}/retry                                  newsposts_review   204 | 404 | 409
GET    /api/newsposts/channels                                          newsposts_review   List<PostizChannelDTO> | 503
POST   /api/newsposts/items/{Id}/publish                                newsposts_review   PostPublicationDTO | 400 | 404 | 409 | 502
GET    /api/newsposts/publications                                      newsposts_review   List<PostPublicationDTO>
GET    /api/newsposts/feeds                                             newsposts_manage   List<NewsFeedDTO>
PUT    /api/newsposts/feeds                                             newsposts_manage   NewsFeedDTO (Id empty = create)
DELETE /api/newsposts/feeds/{Id}                                        newsposts_manage   204 | 409 when items exist
GET    /api/newsposts/sources                                           newsposts_manage   List<ProgrammeSourceDTO>
POST   /api/newsposts/sources                                           newsposts_manage   ProgrammeSourceDTO | 409
DELETE /api/newsposts/sources/{Id}                                      newsposts_manage   204
POST   /api/newsposts/sources/sync                                      newsposts_manage   204 (runs sync in the request)
GET    /api/newsposts/status                                            newsposts_manage   NewsPostsStatusDTO
```

- [ ] **Step 1: Write the DTOs and the failing tests**

`Quartermaster.Api/NewsPosts/NewsPostDTOs.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace Quartermaster.Api.NewsPosts;

/// <summary>Mirrors Quartermaster.Data.NewsPosts.NewsItemStatus (same int values).</summary>
public enum NewsItemStatusDTO {
    New,
    Irrelevant,
    Relevant,
    Generated,
    Posting,
    Posted,
    Dismissed,
    Failed
}

public class NewsItemDTO {
    public Guid Id { get; set; }
    public string FeedName { get; set; } = "";
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public string Teaser { get; set; } = "";
    public bool HasFullText { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public NewsItemStatusDTO Status { get; set; }
    public string? TriageReason { get; set; }
    public string? LastError { get; set; }
    public List<PostCandidateDTO> Candidates { get; set; } = [];
}

public class PostCandidateDTO {
    public Guid Id { get; set; }
    public string Text { get; set; } = "";
    public string SectionHeadingPath { get; set; } = "";
    public string SectionBody { get; set; } = "";
    public string SectionUrl { get; set; } = "";
}

public class PostizChannelDTO {
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Platform { get; set; } = "";
    public string? Profile { get; set; }
    public string? Picture { get; set; }
    public bool Disabled { get; set; }
}

public class PublishRequest {
    public Guid? CandidateId { get; set; }
    public string Text { get; set; } = "";
    public List<string> ChannelIds { get; set; } = [];

    /// <summary>Null publishes now. Must carry an offset so the server can convert it to UTC unambiguously.</summary>
    public DateTimeOffset? ScheduledFor { get; set; }
}

public class PostPublicationDTO {
    public Guid Id { get; set; }
    public Guid NewsItemId { get; set; }
    public string NewsTitle { get; set; } = "";
    public string FinalText { get; set; } = "";
    public List<string> ChannelIds { get; set; } = [];
    public DateTimeOffset? ScheduledFor { get; set; }
    public bool Succeeded { get; set; }
    public string? Error { get; set; }
    public string ApprovedBy { get; set; } = "";
    public DateTimeOffset ApprovedAt { get; set; }
}

public class NewsFeedDTO {
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";

    /// <summary>0 = RSS/Atom, 1 = tagesschau API.</summary>
    public int Kind { get; set; }
    public bool IsEnabled { get; set; }
    public DateTimeOffset? LastPolledAt { get; set; }
    public string? LastError { get; set; }
}

public class ProgrammeSourceDTO {
    public Guid Id { get; set; }
    public string WikiPageTitle { get; set; } = "";
    public DateTimeOffset? LastSyncedAt { get; set; }
    public string? LastError { get; set; }
    public int SectionCount { get; set; }
}

public class ProgrammeSourceCreateRequest {
    public string WikiPageTitle { get; set; } = "";
}

public class NewsPostsStatusDTO {
    public bool Enabled { get; set; }
    public bool MistralConfigured { get; set; }
    public bool PostizConfigured { get; set; }

    /// <summary>True when a live channel listing succeeded; Mistral is only checked for a configured key, since a probe costs tokens.</summary>
    public bool PostizReachable { get; set; }
    public DateTimeOffset? LastRunAt { get; set; }
    public string? LastRunError { get; set; }
    public int IndexedSections { get; set; }
    public Dictionary<string, int> ItemsByStatus { get; set; } = [];
}
```

`Fakes/FakePostizClient.cs`:

```csharp
using Quartermaster.Server.NewsPosts.Postiz;

namespace Quartermaster.Server.Tests.NewsPosts.Fakes;

public class FakePostizClient : IPostizClient {
    public static List<PostizChannel> Channels { get; } = [];
    public static List<(string Text, List<string> ChannelIds, DateTime? At)> Created { get; } = [];
    public static bool FailNext { get; set; }

    public static void Reset() {
        Channels.Clear();
        Created.Clear();
        FailNext = false;
    }

    public Task<List<PostizChannel>> GetChannelsAsync(CancellationToken ct) => Task.FromResult(Channels.ToList());

    public Task<List<string>> CreatePostAsync(string text, IReadOnlyList<PostizChannel> channels, DateTime? scheduledForUtc, CancellationToken ct) {
        if (FailNext) {
            FailNext = false;
            throw new PostizException("Postiz /public/v1/posts returned 500: boom");
        }
        Created.Add((text, channels.Select(c => c.Id).ToList(), scheduledForUtc));
        return Task.FromResult(channels.Select(c => "post-" + c.Id).ToList());
    }
}
```

(The fake keeps static state because the web host resolves a fresh scoped instance per request; each test calls `Reset()` in `[Before(Test)]`. Mark the test class `[NotInParallel("FakePostizClient")]` so the shared state is safe.)

`PublishEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using LinqToDB;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quartermaster.Api;
using Quartermaster.Api.NewsPosts;
using Quartermaster.Data.NewsPosts;
using Quartermaster.Server.NewsPosts.Postiz;
using Quartermaster.Server.Tests.Infrastructure;
using Quartermaster.Server.Tests.NewsPosts.Fakes;

namespace Quartermaster.Server.Tests.NewsPosts;

[NotInParallel("FakePostizClient")]
public class PublishEndpointTests : IntegrationTestBase {
    private WebApplicationFactory<Program> _host = default!;
    private string _token = "";
    private NewsItem _item = default!;
    private PostCandidate _candidate = default!;

    [Before(Test)]
    public void Setup() {
        FakePostizClient.Reset();
        FakePostizClient.Channels.Add(new PostizChannel("x1", "Piraten X", "x", "@piraten", null, false));
        FakePostizClient.Channels.Add(new PostizChannel("m1", "Piraten Mastodon", "mastodon", null, null, false));
        FakePostizClient.Channels.Add(new PostizChannel("off", "Alt", "bluesky", null, null, true));

        _host = Factory.WithWebHostBuilder(b => b.ConfigureServices(services => {
            services.RemoveAll<IPostizClient>();
            services.AddScoped<IPostizClient, FakePostizClient>();
        }));

        (_, _token) = Builder.SeedAuthenticatedUser(globalPermissions: [PermissionIdentifier.ReviewNewsPosts]);

        var feed = new NewsFeed { Name = "f", Url = "https://f", Kind = NewsFeedKind.Rss };
        Db.Insert(feed);
        var now = DateTime.UtcNow;
        _item = new NewsItem {
            FeedId = feed.Id, ExternalId = "e", Url = "https://x", Title = "T", Teaser = "",
            PublishedAt = now, FetchedAt = now, UpdatedAt = now, Status = NewsItemStatus.Generated
        };
        Db.Insert(_item);
        _candidate = new PostCandidate { NewsItemId = _item.Id, Text = "Vorschlag", SectionId = Guid.NewGuid(), CreatedAt = now };
        Db.Insert(_candidate);
    }

    private async Task<HttpClient> ClientAsync() {
        var client = _host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
        var tokenResponse = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/antiforgery/token");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", tokenResponse.GetProperty("token").GetString());
        return client;
    }

    private Task<HttpResponseMessage> Publish(HttpClient client, PublishRequest request)
        => client.PostAsJsonAsync($"/api/newsposts/items/{_item.Id}/publish", request);

    [Test]
    public async Task Publishes_to_selected_channels_and_marks_item_posted() {
        using var client = await ClientAsync();

        var response = await Publish(client, new PublishRequest { CandidateId = _candidate.Id, Text = "Bearbeitet 🏴‍☠️", ChannelIds = ["x1", "m1"] });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(FakePostizClient.Created.Single().ChannelIds).IsEquivalentTo(new List<string> { "x1", "m1" });
        await Assert.That(Db.NewsItems.Single(i => i.Id == _item.Id).Status).IsEqualTo(NewsItemStatus.Posted);
        var publication = Db.PostPublications.Single();
        await Assert.That(publication.Status).IsEqualTo(PostPublicationStatus.Sent);
        await Assert.That(publication.FinalText).IsEqualTo("Bearbeitet 🏴‍☠️");
    }

    [Test]
    public async Task Second_publish_of_same_item_is_rejected_with_409() {
        using var client = await ClientAsync();
        await Publish(client, new PublishRequest { Text = "a", ChannelIds = ["x1"] });

        var second = await Publish(client, new PublishRequest { Text = "a", ChannelIds = ["x1"] });

        await Assert.That(second.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(FakePostizClient.Created.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Text_over_limit_counted_in_text_elements_is_rejected() {
        using var client = await ClientAsync();
        var tooLong = new string('x', 280) + "👍🏽";

        var response = await Publish(client, new PublishRequest { Text = tooLong, ChannelIds = ["x1"] });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(FakePostizClient.Created).IsEmpty();
    }

    [Test]
    public async Task Disabled_or_unknown_channel_is_rejected_and_item_stays_generated() {
        using var client = await ClientAsync();

        var disabled = await Publish(client, new PublishRequest { Text = "a", ChannelIds = ["off"] });
        var unknown = await Publish(client, new PublishRequest { Text = "a", ChannelIds = ["gone"] });

        await Assert.That(disabled.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(unknown.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(FakePostizClient.Created).IsEmpty();
        await Assert.That(Db.NewsItems.Single(i => i.Id == _item.Id).Status).IsEqualTo(NewsItemStatus.Generated);
    }

    [Test]
    public async Task Past_schedule_is_rejected() {
        using var client = await ClientAsync();

        var response = await Publish(client, new PublishRequest { Text = "a", ChannelIds = ["x1"], ScheduledFor = DateTimeOffset.UtcNow.AddMinutes(-5) });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Future_schedule_is_forwarded_as_utc() {
        using var client = await ClientAsync();
        var at = new DateTimeOffset(DateTime.UtcNow.AddDays(1).Date.AddHours(10), TimeSpan.FromHours(2));

        await Publish(client, new PublishRequest { Text = "a", ChannelIds = ["m1"], ScheduledFor = at });

        await Assert.That(FakePostizClient.Created.Single().At).IsEqualTo(at.UtcDateTime);
    }

    [Test]
    public async Task Postiz_failure_records_failed_publication_and_returns_item_to_generated() {
        using var client = await ClientAsync();
        FakePostizClient.FailNext = true;

        var response = await Publish(client, new PublishRequest { Text = "a", ChannelIds = ["x1"] });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadGateway);
        await Assert.That(Db.PostPublications.Single().Status).IsEqualTo(PostPublicationStatus.Failed);
        await Assert.That(Db.NewsItems.Single(i => i.Id == _item.Id).Status).IsEqualTo(NewsItemStatus.Generated);
    }
}
```

(Replace the fully qualified `System.Net.Http.Headers` / `System.Text.Json` names with `using` directives when writing the file.)

`NewsPostsEndpointPermissionTests.cs`:

```csharp
using System.Net;
using Quartermaster.Api;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.NewsPosts;

public class NewsPostsEndpointPermissionTests : IntegrationTestBase {
    [Test]
    [Arguments("/api/newsposts/items?view=suggestions")]
    [Arguments("/api/newsposts/publications")]
    public async Task Review_endpoints_forbid_users_without_review_permission(string url) {
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: [PermissionIdentifier.ManageNewsPosts]);
        using var client = AuthenticatedClient(token);

        var response = await client.GetAsync(url);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    [Arguments("/api/newsposts/feeds")]
    [Arguments("/api/newsposts/sources")]
    [Arguments("/api/newsposts/status")]
    public async Task Settings_endpoints_forbid_reviewers_without_manage_permission(string url) {
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: [PermissionIdentifier.ReviewNewsPosts]);
        using var client = AuthenticatedClient(token);

        var response = await client.GetAsync(url);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Anonymous_gets_401() {
        using var client = AnonymousClient();

        var response = await client.GetAsync("/api/newsposts/items?view=all");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Reviewer_sees_suggestions_list() {
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: [PermissionIdentifier.ReviewNewsPosts]);
        using var client = AuthenticatedClient(token);

        var response = await client.GetAsync("/api/newsposts/items?view=suggestions");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}
```

- [ ] **Step 2: Run to verify failure** (build errors, then 404s once DTOs compile)

- [ ] **Step 3: Implement the endpoints**

Every endpoint follows the `PdfTemplateSaveEndpoint` shape: inject `PermissionContext`, return 401 when `UserId == null`, 403 when `!HasGlobal(<permission>)`. Mapping of items to DTOs lives in one place:

`Endpoints/NewsItemMapper.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Quartermaster.Api.NewsPosts;
using Quartermaster.Data.NewsPosts;

namespace Quartermaster.Server.NewsPosts.Endpoints;

/// <summary>Builds item DTOs with candidates and their cited passages; needs three repositories, so it is a mapper rather than a method on the entity.</summary>
public static class NewsItemMapper {
    public static List<NewsItemDTO> ToDtos(List<NewsItem> items, NewsItemRepository itemRepo, ProgrammeRepository programmeRepo, NewsFeedRepository feedRepo) {
        var feeds = feedRepo.GetAll().ToDictionary(f => f.Id, f => f.Name);
        var candidates = itemRepo.GetCandidatesForItems(items.Select(i => i.Id).ToList());
        var sections = programmeRepo.GetSectionsByIds(candidates.Select(c => c.SectionId).Distinct().ToList()).ToDictionary(s => s.Id);

        return items
            .Select(item => new NewsItemDTO {
                Id = item.Id,
                FeedName = feeds.GetValueOrDefault(item.FeedId, ""),
                Title = item.Title,
                Url = item.Url,
                Teaser = item.Teaser,
                HasFullText = item.HasFullText,
                PublishedAt = new DateTimeOffset(DateTime.SpecifyKind(item.PublishedAt, DateTimeKind.Utc)),
                Status = (NewsItemStatusDTO)(int)item.Status,
                TriageReason = item.TriageReason,
                LastError = item.LastError,
                Candidates = candidates
                    .Where(c => c.NewsItemId == item.Id)
                    .Select(c => ToCandidateDto(c, sections.GetValueOrDefault(c.SectionId)))
                    .ToList()
            })
            .ToList();
    }

    private static PostCandidateDTO ToCandidateDto(PostCandidate candidate, ProgrammeSection? section) {
        return new PostCandidateDTO {
            Id = candidate.Id,
            Text = candidate.Text,
            SectionHeadingPath = section?.HeadingPath ?? "",
            SectionBody = section?.Body ?? "",
            SectionUrl = section == null
                ? ""
                : $"https://wiki.piratenpartei.de/{Uri.EscapeDataString(section.PageTitle.Replace(' ', '_'))}#{Uri.EscapeDataString(section.Anchor)}"
        };
    }
}
```

(`Uri.EscapeDataString` escapes `/` in subpage titles; replace `%2F` back with `/` after escaping.)

`Endpoints/NewsItemListEndpoint.cs`:

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.NewsPosts;
using Quartermaster.Data.NewsPosts;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.NewsPosts.Endpoints;

public class NewsItemListRequest {
    [QueryParam]
    public string View { get; set; } = "suggestions";

    [QueryParam]
    public int? Status { get; set; }
}

public class NewsItemListEndpoint : Endpoint<NewsItemListRequest, List<NewsItemDTO>> {
    private const int Limit = 200;

    private readonly NewsItemRepository _itemRepo;
    private readonly ProgrammeRepository _programmeRepo;
    private readonly NewsFeedRepository _feedRepo;
    private readonly PermissionContext _perms;

    public NewsItemListEndpoint(NewsItemRepository itemRepo, ProgrammeRepository programmeRepo, NewsFeedRepository feedRepo, PermissionContext perms) {
        _itemRepo = itemRepo;
        _programmeRepo = programmeRepo;
        _feedRepo = feedRepo;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/newsposts/items");
    }

    public override async Task HandleAsync(NewsItemListRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.ReviewNewsPosts)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var items = _itemRepo.GetByStatuses(StatusesFor(req), Limit);
        await SendAsync(NewsItemMapper.ToDtos(items, _itemRepo, _programmeRepo, _feedRepo), cancellation: ct);
    }

    private static List<NewsItemStatus> StatusesFor(NewsItemListRequest req) {
        if (req.Status != null)
            return [(NewsItemStatus)req.Status.Value];

        return req.View switch {
            "posted" => [NewsItemStatus.Posted],
            "all" => [
                NewsItemStatus.New, NewsItemStatus.Irrelevant, NewsItemStatus.Relevant, NewsItemStatus.Generated,
                NewsItemStatus.Posting, NewsItemStatus.Posted, NewsItemStatus.Dismissed, NewsItemStatus.Failed
            ],
            _ => [NewsItemStatus.Relevant, NewsItemStatus.Generated]
        };
    }
}
```

`Endpoints/NewsItemGenerateEndpoint.cs` (dismiss and retry follow the same shape; code for all three):

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Data.NewsPosts;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.NewsPosts.Mistral;
using Quartermaster.Server.NewsPosts.Pipeline;

namespace Quartermaster.Server.NewsPosts.Endpoints;

public class NewsItemIdRequest {
    public Guid Id { get; set; }
}

public class NewsItemGenerateEndpoint : Endpoint<NewsItemIdRequest> {
    private readonly NewsPipeline _pipeline;
    private readonly NewsItemRepository _itemRepo;
    private readonly PermissionContext _perms;

    public NewsItemGenerateEndpoint(NewsPipeline pipeline, NewsItemRepository itemRepo, PermissionContext perms) {
        _pipeline = pipeline;
        _itemRepo = itemRepo;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/newsposts/items/{Id}/generate");
    }

    public override async Task HandleAsync(NewsItemIdRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.ReviewNewsPosts)) {
            await SendForbiddenAsync(ct);
            return;
        }
        if (_itemRepo.Get(req.Id) == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        try {
            if (!await _pipeline.GenerateForItemAsync(req.Id, ct)) {
                await SendAsync(new { }, 409, ct);
                return;
            }
        } catch (MistralException ex) {
            Logger.LogWarning(ex, "Manual generation for {Id} failed", req.Id);
            AddError(ex.Message);
            await SendErrorsAsync(502, ct);
            return;
        }
        await SendNoContentAsync(ct);
    }
}
```

`NewsItemDismissEndpoint` (`Post("/api/newsposts/items/{Id}/dismiss")`, reuses `NewsItemIdRequest`): after the permission checks, load the item; 404 if missing; if `Status` is `Posted` or `Posting` return 409; otherwise `_itemRepo.SetStatus(req.Id, NewsItemStatus.Dismissed)` and 204.

`NewsItemRetryEndpoint` (`Post("/api/newsposts/items/{Id}/retry")`): permission checks, 404 if missing, `_pipeline.ResetFailed(req.Id)` false gives 409, else 204.

`Endpoints/NewsChannelListEndpoint.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.Extensions.Logging;
using Quartermaster.Api;
using Quartermaster.Api.NewsPosts;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.NewsPosts.Postiz;

namespace Quartermaster.Server.NewsPosts.Endpoints;

public class NewsChannelListEndpoint : EndpointWithoutRequest<List<PostizChannelDTO>> {
    private readonly IPostizClient _postiz;
    private readonly NewsPostsSettings _settings;
    private readonly PermissionContext _perms;

    public NewsChannelListEndpoint(IPostizClient postiz, NewsPostsSettings settings, PermissionContext perms) {
        _postiz = postiz;
        _settings = settings;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/newsposts/channels");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.ReviewNewsPosts)) {
            await SendForbiddenAsync(ct);
            return;
        }
        if (!_settings.HasPostiz) {
            await SendAsync([], 503, ct);
            return;
        }

        try {
            var channels = await _postiz.GetChannelsAsync(ct);
            await SendAsync(channels
                .Select(c => new PostizChannelDTO { Id = c.Id, Name = c.Name, Platform = c.Identifier, Profile = c.Profile, Picture = c.Picture, Disabled = c.Disabled })
                .ToList(), cancellation: ct);
        } catch (PostizException ex) {
            Logger.LogWarning(ex, "Loading Postiz channels failed");
            await SendAsync([], 502, ct);
        } catch (HttpRequestException ex) {
            Logger.LogWarning(ex, "Loading Postiz channels failed");
            await SendAsync([], 502, ct);
        }
    }
}
```

`Endpoints/NewsPublishEndpoint.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.Extensions.Logging;
using Quartermaster.Api;
using Quartermaster.Api.NewsPosts;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.NewsPosts;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.NewsPosts.Postiz;

namespace Quartermaster.Server.NewsPosts.Endpoints;

public class NewsPublishBody : PublishRequest {
    public Guid Id { get; set; }
}

public class NewsPublishEndpoint : Endpoint<NewsPublishBody, PostPublicationDTO> {
    private readonly NewsItemRepository _itemRepo;
    private readonly PostPublicationRepository _publicationRepo;
    private readonly IPostizClient _postiz;
    private readonly AuditLogRepository _auditLog;
    private readonly PermissionContext _perms;

    public NewsPublishEndpoint(NewsItemRepository itemRepo, PostPublicationRepository publicationRepo, IPostizClient postiz,
        AuditLogRepository auditLog, PermissionContext perms) {
        _itemRepo = itemRepo;
        _publicationRepo = publicationRepo;
        _postiz = postiz;
        _auditLog = auditLog;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/newsposts/items/{Id}/publish");
    }

    public override async Task HandleAsync(NewsPublishBody req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.ReviewNewsPosts)) {
            await SendForbiddenAsync(ct);
            return;
        }
        var item = _itemRepo.Get(req.Id);
        if (item == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var text = req.Text.Trim();
        var error = ValidateRequest(text, req);
        List<PostizChannel> selected = [];
        if (error == null)
            (selected, error) = await ResolveChannelsAsync(req.ChannelIds, ct);
        if (error != null) {
            AddError(error);
            await SendErrorsAsync(400, ct);
            return;
        }

        if (!_itemRepo.TryClaimForPosting(item.Id)) {
            AddError("Diese Meldung wird bereits veröffentlicht oder wurde schon veröffentlicht.");
            await SendErrorsAsync(409, ct);
            return;
        }

        var scheduledUtc = req.ScheduledFor?.UtcDateTime;
        var publication = new PostPublication {
            NewsItemId = item.Id,
            CandidateId = req.CandidateId,
            FinalText = text,
            ChannelIds = JsonSerializer.Serialize(selected.Select(c => c.Id)),
            ScheduledFor = scheduledUtc,
            ApprovedByUserId = _perms.UserId.Value,
            ApprovedAt = DateTime.UtcNow
        };

        try {
            var postIds = await _postiz.CreatePostAsync(text, selected, scheduledUtc, ct);
            publication.PostizPostIds = JsonSerializer.Serialize(postIds);
            publication.Status = PostPublicationStatus.Sent;
            _publicationRepo.Create(publication);
            _itemRepo.SetStatus(item.Id, NewsItemStatus.Posted);
            _auditLog.Log("NewsItem", item.Id, "Published", newValue: publication.ChannelIds);
        } catch (Exception ex) when (ex is PostizException || ex is HttpRequestException || ex is TaskCanceledException) {
            Logger.LogError(ex, "Publishing news item {Id} to Postiz failed", item.Id);
            publication.Status = PostPublicationStatus.Failed;
            publication.Error = ex.Message;
            _publicationRepo.Create(publication);
            _itemRepo.SetStatus(item.Id, NewsItemStatus.Generated);
            AddError("Postiz hat die Veröffentlichung abgelehnt oder nicht beantwortet. Bitte in Postiz prüfen, bevor du es erneut versuchst.");
            await SendErrorsAsync(502, ct);
            return;
        }

        await SendAsync(new PostPublicationDTO {
            Id = publication.Id,
            NewsItemId = item.Id,
            NewsTitle = item.Title,
            FinalText = text,
            ChannelIds = selected.Select(c => c.Id).ToList(),
            ScheduledFor = req.ScheduledFor,
            Succeeded = true,
            ApprovedAt = new DateTimeOffset(publication.ApprovedAt, TimeSpan.Zero)
        }, cancellation: ct);
    }

    private static string? ValidateRequest(string text, PublishRequest req) {
        if (!PostText.IsValid(text))
            return $"Der Text muss zwischen 1 und {PostText.MaxLength} Zeichen lang sein.";
        if (req.ChannelIds.Count == 0)
            return "Bitte mindestens einen Kanal auswählen.";
        if (req.ScheduledFor != null && req.ScheduledFor.Value <= DateTimeOffset.UtcNow)
            return "Der geplante Zeitpunkt liegt in der Vergangenheit.";
        return null;
    }

    private async Task<(List<PostizChannel> Channels, string? Error)> ResolveChannelsAsync(List<string> ids, CancellationToken ct) {
        var available = (await _postiz.GetChannelsAsync(ct)).ToDictionary(c => c.Id);
        var selected = new List<PostizChannel>();
        foreach (var id in ids.Distinct()) {
            if (!available.TryGetValue(id, out var channel) || channel.Disabled)
                return ([], "Ein ausgewählter Kanal ist in Postiz nicht (mehr) verfügbar. Bitte die Kanäle neu laden.");
            selected.Add(channel);
        }
        return (selected, null);
    }
}
```

(The `catch ... when` with three `is` checks uses `||`, which `CLAUDE.md` allows; if it reads long, split into three catch blocks calling one private `HandleFailureAsync`.)

`Endpoints/NewsPublicationListEndpoint.cs`: `EndpointWithoutRequest<List<PostPublicationDTO>>`, `Get("/api/newsposts/publications")`, review permission; loads `_publicationRepo.GetRecent(200)`, the related items via `_itemRepo.Get` per distinct id (at most 200), and approver names via `UserRepository.Get(ApprovedByUserId)` (`FirstName + " " + LastName`); maps `ChannelIds` with `JsonSerializer.Deserialize<List<string>>`, `Succeeded = Status == Sent`.

Settings endpoints, all gated by `PermissionIdentifier.ManageNewsPosts`:

- `NewsFeedListEndpoint`: `Get("/api/newsposts/feeds")`, maps every `NewsFeed` to `NewsFeedDTO` (`Kind = (int)feed.Kind`, dates as UTC offsets).
- `NewsFeedSaveEndpoint`: `Endpoint<NewsFeedDTO, NewsFeedDTO>`, `Put("/api/newsposts/feeds")`. Validate: `Name` non-blank, `Url` an absolute http(s) URI, `Kind` 0 or 1 (else 400 with German messages). `Id == Guid.Empty` creates; otherwise loads (404 if missing), updates Name/Url/Kind/IsEnabled, saves. Returns the saved DTO.
- `NewsFeedDeleteEndpoint`: `Delete("/api/newsposts/feeds/{Id}")`; 409 with "Diese Quelle hat bereits Meldungen. Deaktiviere sie stattdessen." when `Db.NewsItems.Any(i => i.FeedId == id)` (add `bool HasItems(Guid feedId)` to `NewsItemRepository`), else delete and 204.
- `ProgrammeSourceListEndpoint`: `Get("/api/newsposts/sources")` with `SectionCount = _programmeRepo.CountSections(s.Id)`.
- `ProgrammeSourceCreateEndpoint`: `Endpoint<ProgrammeSourceCreateRequest, ProgrammeSourceDTO>`, `Post("/api/newsposts/sources")`; trims the title, 400 when blank, 409 when `SourceExists`, else creates.
- `ProgrammeSourceDeleteEndpoint`: `Delete("/api/newsposts/sources/{Id}")` calls `DeleteSource`, then `ProgrammeSyncService.LoadIndex()`, 204.
- `ProgrammeSyncEndpoint`: `Post("/api/newsposts/sources/sync")`; 409 with "Mistral ist nicht konfiguriert." when `!settings.HasMistral`; otherwise `await _syncService.SyncAllAsync(ct)` and 204. A full first sync embeds ~700 sections and can take a minute; the Blazor button shows a spinner meanwhile.
- `NewsPostsStatusEndpoint`: `Get("/api/newsposts/status")` fills `NewsPostsStatusDTO` from settings (`Enabled`, `HasMistral`, `HasPostiz`), `PostizReachable` from a `GetChannelsAsync` call wrapped in try/catch (false on `PostizException`/`HttpRequestException`, skipped when not configured), `NewsPipelineHostedService.LastRunAt/LastRunError`, `ProgrammeIndex.Count`, and `_itemRepo.CountByStatus()` keyed by the enum name.

The antiforgery middleware covers every non-GET under `/api`; the Blazor `CsrfDelegatingHandler` already adds the header, so no extra work.

- [ ] **Step 4: Run tests** (`PublishEndpointTests`, `NewsPostsEndpointPermissionTests`): all pass.

- [ ] **Step 5: Checkpoint** (user commits)

---

### Task 10: Blazor pages, navigation, i18n, changelog

**Files:**
- Create: `Quartermaster.Blazor/Pages/Administration/NewsPosts.razor`, `NewsPosts.razor.cs`, `NewsPostsSettings.razor`, `NewsPostsSettings.razor.cs`
- Modify: `Quartermaster.Blazor/Layout/MainNavBar.razor`
- Modify: `Quartermaster.Api/I18n/I18nKey.cs`, `Quartermaster.Server/wwwroot/i18n/de.json`, `en.json`
- Modify: `Quartermaster.Server/Changelog/V3.md`

**Interfaces:**
- Consumes: the API and DTOs from Task 9, `PostText` (Task 1), existing components `RequirePermission`, `LoadingSpinner`, `ConfirmDialog`, `LocalTime`, `ToastService`, `I18nService`.

- [ ] **Step 1: i18n keys**

`I18nKey.cs`, inside `Ui.MainNavBar`: `public const string BoardWorkNewsPosts = "ui.main_nav_bar.board_work.news_posts";` and a new class:

```csharp
        public static class NewsPosts {
            public const string PageTitle = "ui.news_posts.page_title";
            public const string TabSuggestions = "ui.news_posts.tab_suggestions";
            public const string TabAll = "ui.news_posts.tab_all";
            public const string TabPosted = "ui.news_posts.tab_posted";
            public const string SettingsLink = "ui.news_posts.settings_link";
            public const string TeaserOnly = "ui.news_posts.teaser_only";
            public const string NoCandidates = "ui.news_posts.no_candidates";
            public const string EmptyQueue = "ui.news_posts.empty_queue";
            public const string ProgrammePassage = "ui.news_posts.programme_passage";
            public const string UseCandidate = "ui.news_posts.use_candidate";
            public const string Dismiss = "ui.news_posts.dismiss";
            public const string Regenerate = "ui.news_posts.regenerate";
            public const string Generate = "ui.news_posts.generate";
            public const string Retry = "ui.news_posts.retry";
            public const string Channels = "ui.news_posts.channels";
            public const string ChannelsUnavailable = "ui.news_posts.channels_unavailable";
            public const string PublishNow = "ui.news_posts.publish_now";
            public const string PublishAt = "ui.news_posts.publish_at";
            public const string PublishButton = "ui.news_posts.publish_button";
            public const string PublishConfirm = "ui.news_posts.publish_confirm";
            public const string Published = "ui.news_posts.published";
            public const string Cancel = "ui.news_posts.cancel";
            public const string StatusFilterAll = "ui.news_posts.status_filter_all";
            public const string PostedAt = "ui.news_posts.posted_at";
            public const string PostedBy = "ui.news_posts.posted_by";
            public const string PostFailed = "ui.news_posts.post_failed";
            public const string SettingsTitle = "ui.news_posts.settings_title";
            public const string FeedsTitle = "ui.news_posts.feeds_title";
            public const string FeedName = "ui.news_posts.feed_name";
            public const string FeedUrl = "ui.news_posts.feed_url";
            public const string FeedKind = "ui.news_posts.feed_kind";
            public const string FeedKindRss = "ui.news_posts.feed_kind_rss";
            public const string FeedKindTagesschau = "ui.news_posts.feed_kind_tagesschau";
            public const string FeedEnabled = "ui.news_posts.feed_enabled";
            public const string FeedLastPoll = "ui.news_posts.feed_last_poll";
            public const string FeedAdd = "ui.news_posts.feed_add";
            public const string Save = "ui.news_posts.save";
            public const string Delete = "ui.news_posts.delete";
            public const string SourcesTitle = "ui.news_posts.sources_title";
            public const string SourceTitle = "ui.news_posts.source_title";
            public const string SourceHint = "ui.news_posts.source_hint";
            public const string SourceSections = "ui.news_posts.source_sections";
            public const string SourceLastSync = "ui.news_posts.source_last_sync";
            public const string SourceAdd = "ui.news_posts.source_add";
            public const string SyncNow = "ui.news_posts.sync_now";
            public const string SyncDone = "ui.news_posts.sync_done";
            public const string StatusTitle = "ui.news_posts.status_title";
            public const string StatusEnabled = "ui.news_posts.status_enabled";
            public const string StatusMistral = "ui.news_posts.status_mistral";
            public const string StatusPostiz = "ui.news_posts.status_postiz";
            public const string StatusPostizReachable = "ui.news_posts.status_postiz_reachable";
            public const string StatusLastRun = "ui.news_posts.status_last_run";
            public const string StatusSections = "ui.news_posts.status_sections";
            public const string Yes = "ui.news_posts.yes";
            public const string No = "ui.news_posts.no";
        }
```

`de.json` entries (insert next to the other `ui.news...`/`ui.main_nav_bar.board_work.*` keys; keep the file's existing ordering style):

```json
  "ui.main_nav_bar.board_work.news_posts": "Social Media",
  "ui.news_posts.page_title": "Social Media",
  "ui.news_posts.tab_suggestions": "Vorschläge",
  "ui.news_posts.tab_all": "Alle Meldungen",
  "ui.news_posts.tab_posted": "Veröffentlicht",
  "ui.news_posts.settings_link": "Einstellungen",
  "ui.news_posts.teaser_only": "Nur Teaser",
  "ui.news_posts.no_candidates": "Keine passenden Vorschläge",
  "ui.news_posts.empty_queue": "Aktuell liegen keine Vorschläge vor.",
  "ui.news_posts.programme_passage": "Programmstelle",
  "ui.news_posts.use_candidate": "Übernehmen",
  "ui.news_posts.dismiss": "Verwerfen",
  "ui.news_posts.regenerate": "Neu generieren",
  "ui.news_posts.generate": "Generieren",
  "ui.news_posts.retry": "Erneut versuchen",
  "ui.news_posts.channels": "Kanäle",
  "ui.news_posts.channels_unavailable": "Die Kanäle konnten nicht aus Postiz geladen werden.",
  "ui.news_posts.publish_now": "Sofort",
  "ui.news_posts.publish_at": "Geplant",
  "ui.news_posts.publish_button": "Freigeben & veröffentlichen",
  "ui.news_posts.publish_confirm": "Diesen Beitrag jetzt an die ausgewählten Kanäle senden?",
  "ui.news_posts.published": "Beitrag an Postiz übergeben.",
  "ui.news_posts.cancel": "Abbrechen",
  "ui.news_posts.status_filter_all": "Alle Status",
  "ui.news_posts.posted_at": "Freigegeben am",
  "ui.news_posts.posted_by": "Freigegeben von",
  "ui.news_posts.post_failed": "Fehlgeschlagen",
  "ui.news_posts.settings_title": "Social Media: Einstellungen",
  "ui.news_posts.feeds_title": "Nachrichtenquellen",
  "ui.news_posts.feed_name": "Name",
  "ui.news_posts.feed_url": "URL",
  "ui.news_posts.feed_kind": "Art",
  "ui.news_posts.feed_kind_rss": "RSS/Atom",
  "ui.news_posts.feed_kind_tagesschau": "tagesschau-API",
  "ui.news_posts.feed_enabled": "Aktiv",
  "ui.news_posts.feed_last_poll": "Letzter Abruf",
  "ui.news_posts.feed_add": "Quelle hinzufügen",
  "ui.news_posts.save": "Speichern",
  "ui.news_posts.delete": "Löschen",
  "ui.news_posts.sources_title": "Programmquellen",
  "ui.news_posts.source_title": "Wiki-Seite",
  "ui.news_posts.source_hint": "Seitentitel im Piratenwiki; mit / am Ende für alle Unterseiten.",
  "ui.news_posts.source_sections": "Abschnitte",
  "ui.news_posts.source_last_sync": "Letzte Synchronisierung",
  "ui.news_posts.source_add": "Seite hinzufügen",
  "ui.news_posts.sync_now": "Jetzt synchronisieren",
  "ui.news_posts.sync_done": "Programm synchronisiert.",
  "ui.news_posts.status_title": "Status",
  "ui.news_posts.status_enabled": "Aktiviert",
  "ui.news_posts.status_mistral": "Mistral konfiguriert",
  "ui.news_posts.status_postiz": "Postiz konfiguriert",
  "ui.news_posts.status_postiz_reachable": "erreichbar",
  "ui.news_posts.status_last_run": "Letzter Durchlauf",
  "ui.news_posts.status_sections": "Indexierte Abschnitte",
  "ui.news_posts.yes": "Ja",
  "ui.news_posts.no": "Nein",
```

`en.json`: the same keys with English values (Social Media, Suggestions, All items, Published, Settings, Teaser only, No matching suggestions, No suggestions right now., Programme passage, Use, Dismiss, Regenerate, Generate, Retry, Channels, Could not load channels from Postiz., Now, Scheduled, Approve & publish, Send this post to the selected channels now?, Post handed to Postiz., Cancel, All statuses, Approved at, Approved by, Failed, Social Media: Settings, News sources, Name, URL, Type, RSS/Atom, tagesschau API, Active, Last poll, Add source, Save, Delete, Programme sources, Wiki page, Page title in the Piratenwiki; end with / for all subpages., Sections, Last sync, Add page, Sync now, Programme synced., Status, Enabled, Mistral configured, Postiz configured, reachable, Last run, Indexed sections, Yes, No).

Status labels: add `ui.news_posts.status.<name>` for each `NewsItemStatusDTO` value with German `Neu, Nicht relevant, Relevant, Vorschläge erzeugt, Wird veröffentlicht, Veröffentlicht, Verworfen, Fehlgeschlagen` (English: New, Not relevant, Relevant, Generated, Publishing, Published, Dismissed, Failed) and use `I18n[$"ui.news_posts.status.{status.ToString().ToLowerInvariant()}"]` in the pages.

- [ ] **Step 2: Navigation**

`MainNavBar.razor`, inside the "Vorstandsarbeit" dropdown after the Mailings block:

```razor
                                <RequirePermission GlobalPermission="@PermissionIdentifier.ReviewNewsPosts">
                                    <li><hr class="dropdown-divider"></li>
                                    <li><a class="dropdown-item" href="/Administration/NewsPosts">@I18n[I18nKey.Ui.MainNavBar.BoardWorkNewsPosts]</a></li>
                                </RequirePermission>
```

Check how the "Vorstandsarbeit" dropdown itself is gated: if it only renders for chapter officers, a global reviewer without chapter permissions would not see it. If so, extend that outer condition to also show for `ReviewNewsPosts`.

- [ ] **Step 3: Review page**

`NewsPosts.razor`:

```razor
@page "/Administration/NewsPosts"
@using Quartermaster.Api
@using Quartermaster.Api.I18n
@using Quartermaster.Api.NewsPosts

<PageTitle>@I18n[I18nKey.Ui.NewsPosts.PageTitle]</PageTitle>

<RequirePermission GlobalPermission="@PermissionIdentifier.ReviewNewsPosts">
    <div class="d-flex align-items-center justify-content-between mb-3">
        <h3 class="mb-0">@I18n[I18nKey.Ui.NewsPosts.PageTitle]</h3>
        <RequirePermission GlobalPermission="@PermissionIdentifier.ManageNewsPosts">
            <a class="btn btn-outline-secondary btn-sm" href="/Administration/NewsPosts/Settings">
                <i class="bi bi-gear me-1"></i>@I18n[I18nKey.Ui.NewsPosts.SettingsLink]
            </a>
        </RequirePermission>
    </div>

    <ul class="nav nav-tabs mb-3">
        <li class="nav-item"><button type="button" class="nav-link @(view == "suggestions" ? "active" : "")" @onclick='() => SwitchViewAsync("suggestions")'>@I18n[I18nKey.Ui.NewsPosts.TabSuggestions]</button></li>
        <li class="nav-item"><button type="button" class="nav-link @(view == "all" ? "active" : "")" @onclick='() => SwitchViewAsync("all")'>@I18n[I18nKey.Ui.NewsPosts.TabAll]</button></li>
        <li class="nav-item"><button type="button" class="nav-link @(view == "posted" ? "active" : "")" @onclick='() => SwitchViewAsync("posted")'>@I18n[I18nKey.Ui.NewsPosts.TabPosted]</button></li>
    </ul>

    @if (loading) {
        <LoadingSpinner />
    } else if (view == "posted") {
        <div class="table-responsive">
            <table class="table table-striped align-middle">
                <thead>
                    <tr>
                        <th>@I18n[I18nKey.Ui.NewsPosts.PostedAt]</th>
                        <th></th>
                        <th>@I18n[I18nKey.Ui.NewsPosts.Channels]</th>
                        <th>@I18n[I18nKey.Ui.NewsPosts.PostedBy]</th>
                        <th></th>
                    </tr>
                </thead>
                <tbody>
                    @foreach (var publication in publications) {
                        <tr>
                            <td><LocalTime Value="publication.ApprovedAt" Format="dd.MM.yyyy HH:mm" /></td>
                            <td><div class="fw-semibold">@publication.NewsTitle</div><div class="small">@publication.FinalText</div></td>
                            <td>@string.Join(", ", publication.ChannelIds.Select(ChannelName))</td>
                            <td>@publication.ApprovedBy</td>
                            <td>
                                @if (!publication.Succeeded) {
                                    <span class="badge text-bg-danger" title="@publication.Error">@I18n[I18nKey.Ui.NewsPosts.PostFailed]</span>
                                    <button type="button" class="btn btn-link btn-sm" @onclick="() => RetryPublication(publication)">@I18n[I18nKey.Ui.NewsPosts.Retry]</button>
                                }
                            </td>
                        </tr>
                    }
                </tbody>
            </table>
        </div>
    } else {
        @if (view == "all") {
            <select class="form-select form-select-sm w-auto mb-3" value="@statusFilter" @onchange="OnStatusFilterChanged">
                <option value="">@I18n[I18nKey.Ui.NewsPosts.StatusFilterAll]</option>
                @foreach (var status in Enum.GetValues<NewsItemStatusDTO>()) {
                    <option value="@((int)status)">@StatusLabel(status)</option>
                }
            </select>
        }

        @if (items.Count == 0) {
            <p class="text-secondary">@I18n[I18nKey.Ui.NewsPosts.EmptyQueue]</p>
        }

        @foreach (var item in items) {
            <div class="card mb-3">
                <div class="card-body">
                    <div class="d-flex justify-content-between gap-2 flex-wrap">
                        <div>
                            <a class="h5 d-block mb-1" href="@item.Url" target="_blank" rel="noopener">@item.Title</a>
                            <div class="small text-secondary">
                                @item.FeedName &middot; <LocalTime Value="item.PublishedAt" Format="dd.MM.yyyy HH:mm" />
                                @if (!item.HasFullText) {
                                    <span class="badge text-bg-warning ms-1">@I18n[I18nKey.Ui.NewsPosts.TeaserOnly]</span>
                                }
                                <span class="badge text-bg-secondary ms-1">@StatusLabel(item.Status)</span>
                            </div>
                            @if (!string.IsNullOrEmpty(item.TriageReason)) {
                                <div class="small mt-1">@item.TriageReason</div>
                            }
                            @if (!string.IsNullOrEmpty(item.LastError)) {
                                <div class="small text-danger mt-1">@item.LastError</div>
                            }
                        </div>
                        <div class="d-flex gap-2 align-items-start">
                            @if (item.Status == NewsItemStatusDTO.Failed) {
                                <button type="button" class="btn btn-outline-secondary btn-sm" disabled="@busy" @onclick="() => PostActionAsync(item, "retry")">@I18n[I18nKey.Ui.NewsPosts.Retry]</button>
                            } else if (CanGenerate(item)) {
                                <button type="button" class="btn btn-outline-primary btn-sm" disabled="@busy" @onclick="() => PostActionAsync(item, "generate")">
                                    @(item.Status == NewsItemStatusDTO.Generated ? I18n[I18nKey.Ui.NewsPosts.Regenerate] : I18n[I18nKey.Ui.NewsPosts.Generate])
                                </button>
                            }
                            @if (CanDismiss(item)) {
                                <button type="button" class="btn btn-outline-danger btn-sm" disabled="@busy" @onclick="() => PostActionAsync(item, "dismiss")">@I18n[I18nKey.Ui.NewsPosts.Dismiss]</button>
                            }
                        </div>
                    </div>

                    @if (item.Status == NewsItemStatusDTO.Generated && item.Candidates.Count == 0) {
                        <p class="text-secondary mt-3 mb-0">@I18n[I18nKey.Ui.NewsPosts.NoCandidates]</p>
                    }

                    @foreach (var candidate in item.Candidates) {
                        <div class="border rounded p-2 mt-3">
                            <div>@candidate.Text</div>
                            <div class="d-flex justify-content-between align-items-center mt-1">
                                <span class="small text-secondary">@PostText.Length(candidate.Text) / @PostText.MaxLength</span>
                                <button type="button" class="btn btn-primary btn-sm" @onclick="() => OpenEditor(item, candidate)">@I18n[I18nKey.Ui.NewsPosts.UseCandidate]</button>
                            </div>
                            <details class="small mt-1">
                                <summary>@I18n[I18nKey.Ui.NewsPosts.ProgrammePassage]: @candidate.SectionHeadingPath</summary>
                                <p class="mb-1">@candidate.SectionBody</p>
                                <a href="@candidate.SectionUrl" target="_blank" rel="noopener">@candidate.SectionUrl</a>
                            </details>
                        </div>
                    }

                    @if (editingItemId == item.Id) {
                        <div class="border border-primary rounded p-3 mt-3">
                            <textarea class="form-control" rows="4" @bind="editText" @bind:event="oninput"></textarea>
                            <div class="small mt-1 @(PostText.IsValid(editText) ? "text-secondary" : "text-danger")">@PostText.Length(editText) / @PostText.MaxLength</div>

                            <div class="mt-2">
                                <div class="fw-semibold small">@I18n[I18nKey.Ui.NewsPosts.Channels]</div>
                                @if (channels == null) {
                                    <div class="text-danger small">@I18n[I18nKey.Ui.NewsPosts.ChannelsUnavailable]</div>
                                } else {
                                    @foreach (var channel in channels) {
                                        <div class="form-check">
                                            <input class="form-check-input" type="checkbox" id="ch-@channel.Id" disabled="@channel.Disabled"
                                                   checked="@selectedChannels.Contains(channel.Id)" @onchange="e => ToggleChannel(channel.Id, (bool)e.Value!)" />
                                            <label class="form-check-label @(channel.Disabled ? "text-secondary" : "")" for="ch-@channel.Id">@channel.Name (@channel.Platform)</label>
                                        </div>
                                    }
                                }
                            </div>

                            <div class="mt-2 d-flex gap-3 align-items-center flex-wrap">
                                <div class="form-check">
                                    <input class="form-check-input" type="radio" id="when-now" checked="@(!scheduled)" @onchange="() => scheduled = false" />
                                    <label class="form-check-label" for="when-now">@I18n[I18nKey.Ui.NewsPosts.PublishNow]</label>
                                </div>
                                <div class="form-check">
                                    <input class="form-check-input" type="radio" id="when-at" checked="@scheduled" @onchange="() => scheduled = true" />
                                    <label class="form-check-label" for="when-at">@I18n[I18nKey.Ui.NewsPosts.PublishAt]</label>
                                </div>
                                @if (scheduled) {
                                    <input type="datetime-local" class="form-control form-control-sm w-auto" @bind="scheduledLocal" />
                                }
                            </div>

                            <div class="mt-3 d-flex gap-2">
                                <button type="button" class="btn btn-success" disabled="@(!CanPublish)" @onclick="PublishAsync">@I18n[I18nKey.Ui.NewsPosts.PublishButton]</button>
                                <button type="button" class="btn btn-outline-secondary" @onclick="CloseEditor">@I18n[I18nKey.Ui.NewsPosts.Cancel]</button>
                            </div>
                        </div>
                    }
                </div>
            </div>
        }
    }

    <ConfirmDialog @ref="confirmDialog" Message="@I18n[I18nKey.Ui.NewsPosts.PublishConfirm]" ConfirmText="@I18n[I18nKey.Ui.NewsPosts.PublishButton]" />
</RequirePermission>
```

`NewsPosts.razor.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.NewsPosts;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class NewsPosts {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }

    private readonly HashSet<string> selectedChannels = [];

    private string view = "suggestions";
    private string statusFilter = "";
    private bool loading = true;
    private bool busy;
    private List<NewsItemDTO> items = [];
    private List<PostPublicationDTO> publications = [];
    private List<PostizChannelDTO>? channels;
    private Guid? editingItemId;
    private Guid? editingCandidateId;
    private string editText = "";
    private bool scheduled;
    private DateTime scheduledLocal = DateTime.Now.AddHours(1);
    private ConfirmDialog? confirmDialog;

    private bool CanPublish => !busy && PostText.IsValid(editText) && selectedChannels.Count > 0;

    protected override async Task OnInitializedAsync() {
        await LoadAsync();
        await LoadChannelsAsync();
    }

    private async Task SwitchViewAsync(string newView) {
        view = newView;
        statusFilter = "";
        CloseEditor();
        await LoadAsync();
    }

    private async Task OnStatusFilterChanged(ChangeEventArgs e) {
        statusFilter = e.Value?.ToString() ?? "";
        await LoadAsync();
    }

    private async Task LoadAsync() {
        loading = true;
        try {
            if (view == "posted") {
                publications = await Http.GetFromJsonAsync<List<PostPublicationDTO>>("/api/newsposts/publications") ?? [];
            } else {
                var url = $"/api/newsposts/items?view={view}";
                if (statusFilter.Length > 0)
                    url += "&status=" + statusFilter;
                items = await Http.GetFromJsonAsync<List<NewsItemDTO>>(url) ?? [];
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        loading = false;
    }

    private async Task LoadChannelsAsync() {
        try {
            channels = await Http.GetFromJsonAsync<List<PostizChannelDTO>>("/api/newsposts/channels");
        } catch (HttpRequestException) {
            channels = null;
        }
    }

    private async Task PostActionAsync(NewsItemDTO item, string action) {
        busy = true;
        try {
            var response = await Http.PostAsync($"/api/newsposts/items/{item.Id}/{action}", null);
            if (!response.IsSuccessStatusCode)
                await ToastService.ErrorAsync(response);
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        busy = false;
        await LoadAsync();
    }

    private void OpenEditor(NewsItemDTO item, PostCandidateDTO candidate) {
        editingItemId = item.Id;
        editingCandidateId = candidate.Id;
        editText = candidate.Text;
        scheduled = false;
        selectedChannels.Clear();
    }

    private void RetryPublication(PostPublicationDTO publication) {
        view = "all";
        editingItemId = publication.NewsItemId;
        editingCandidateId = null;
        editText = publication.FinalText;
        selectedChannels.Clear();
        foreach (var id in publication.ChannelIds)
            selectedChannels.Add(id);
        scheduled = publication.ScheduledFor != null && publication.ScheduledFor > DateTimeOffset.Now;
        if (scheduled)
            scheduledLocal = publication.ScheduledFor!.Value.LocalDateTime;
        _ = LoadAsync();
    }

    private void CloseEditor() {
        editingItemId = null;
        editingCandidateId = null;
        editText = "";
    }

    private void ToggleChannel(string id, bool isChecked) {
        if (isChecked) {
            selectedChannels.Add(id);
        } else {
            selectedChannels.Remove(id);
        }
    }

    private async Task PublishAsync() {
        if (editingItemId == null || confirmDialog == null)
            return;
        if (!await confirmDialog.ShowAsync())
            return;

        busy = true;
        var request = new PublishRequest {
            CandidateId = editingCandidateId,
            Text = editText.Trim(),
            ChannelIds = selectedChannels.ToList(),
            ScheduledFor = scheduled ? new DateTimeOffset(scheduledLocal) : null
        };
        try {
            var response = await Http.PostAsJsonAsync($"/api/newsposts/items/{editingItemId}/publish", request);
            if (response.IsSuccessStatusCode) {
                ToastService.ToastKey(I18nKey.Ui.NewsPosts.Published);
                CloseEditor();
            } else {
                await ToastService.ErrorAsync(response);
                await LoadChannelsAsync();
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        busy = false;
        await LoadAsync();
    }

    private static bool CanGenerate(NewsItemDTO item) {
        if (item.Status == NewsItemStatusDTO.Posted)
            return false;
        if (item.Status == NewsItemStatusDTO.Posting)
            return false;
        return item.Status != NewsItemStatusDTO.Failed;
    }

    private static bool CanDismiss(NewsItemDTO item) {
        if (item.Status == NewsItemStatusDTO.Posted)
            return false;
        if (item.Status == NewsItemStatusDTO.Posting)
            return false;
        return item.Status != NewsItemStatusDTO.Dismissed;
    }

    private string StatusLabel(NewsItemStatusDTO status) => I18n[$"ui.news_posts.status.{status.ToString().ToLowerInvariant()}"];

    private string ChannelName(string id) => channels?.FirstOrDefault(c => c.Id == id)?.Name ?? id;
}
```

(`new DateTimeOffset(scheduledLocal)` uses the browser's local offset because `scheduledLocal` has `DateTimeKind.Unspecified` from the input; set `scheduledLocal = DateTime.SpecifyKind(value, DateTimeKind.Local)` in a property setter if the offset comes out as zero during the manual check.)

- [ ] **Step 4: Settings page**

`NewsPostsSettings.razor`:

```razor
@page "/Administration/NewsPosts/Settings"
@using Quartermaster.Api
@using Quartermaster.Api.I18n
@using Quartermaster.Api.NewsPosts

<PageTitle>@I18n[I18nKey.Ui.NewsPosts.SettingsTitle]</PageTitle>

<RequirePermission GlobalPermission="@PermissionIdentifier.ManageNewsPosts">
    <PageBackLink Href="/Administration/NewsPosts" />
    <h3 class="mb-3">@I18n[I18nKey.Ui.NewsPosts.SettingsTitle]</h3>

    @if (status != null) {
        <div class="card mb-4">
            <div class="card-body">
                <h5>@I18n[I18nKey.Ui.NewsPosts.StatusTitle]</h5>
                <div>@I18n[I18nKey.Ui.NewsPosts.StatusEnabled]: @YesNo(status.Enabled)</div>
                <div>@I18n[I18nKey.Ui.NewsPosts.StatusMistral]: @YesNo(status.MistralConfigured)</div>
                <div>@I18n[I18nKey.Ui.NewsPosts.StatusPostiz]: @YesNo(status.PostizConfigured) / @I18n[I18nKey.Ui.NewsPosts.StatusPostizReachable]: @YesNo(status.PostizReachable)</div>
                <div>@I18n[I18nKey.Ui.NewsPosts.StatusSections]: @status.IndexedSections</div>
                <div>
                    @I18n[I18nKey.Ui.NewsPosts.StatusLastRun]:
                    @if (status.LastRunAt != null) {
                        <LocalTime Value="status.LastRunAt.Value" Format="dd.MM.yyyy HH:mm" />
                    } else {
                        <span>-</span>
                    }
                    @if (!string.IsNullOrEmpty(status.LastRunError)) {
                        <span class="text-danger ms-2">@status.LastRunError</span>
                    }
                </div>
                <div class="small text-secondary mt-1">@string.Join(" · ", status.ItemsByStatus.Select(kv => $"{kv.Key}: {kv.Value}"))</div>
            </div>
        </div>
    }

    <h5>@I18n[I18nKey.Ui.NewsPosts.FeedsTitle]</h5>
    <div class="table-responsive mb-4">
        <table class="table align-middle">
            <thead>
                <tr>
                    <th>@I18n[I18nKey.Ui.NewsPosts.FeedName]</th>
                    <th>@I18n[I18nKey.Ui.NewsPosts.FeedUrl]</th>
                    <th>@I18n[I18nKey.Ui.NewsPosts.FeedKind]</th>
                    <th>@I18n[I18nKey.Ui.NewsPosts.FeedEnabled]</th>
                    <th>@I18n[I18nKey.Ui.NewsPosts.FeedLastPoll]</th>
                    <th></th>
                </tr>
            </thead>
            <tbody>
                @foreach (var feed in feeds) {
                    <tr>
                        <td><input class="form-control form-control-sm" @bind="feed.Name" /></td>
                        <td><input class="form-control form-control-sm" @bind="feed.Url" /></td>
                        <td>
                            <select class="form-select form-select-sm" @bind="feed.Kind">
                                <option value="0">@I18n[I18nKey.Ui.NewsPosts.FeedKindRss]</option>
                                <option value="1">@I18n[I18nKey.Ui.NewsPosts.FeedKindTagesschau]</option>
                            </select>
                        </td>
                        <td><input type="checkbox" class="form-check-input" @bind="feed.IsEnabled" /></td>
                        <td class="small">
                            @if (feed.LastPolledAt != null) {
                                <LocalTime Value="feed.LastPolledAt.Value" Format="dd.MM. HH:mm" />
                            }
                            @if (!string.IsNullOrEmpty(feed.LastError)) {
                                <div class="text-danger">@feed.LastError</div>
                            }
                        </td>
                        <td class="text-end text-nowrap">
                            <button type="button" class="btn btn-outline-primary btn-sm" @onclick="() => SaveFeedAsync(feed)">@I18n[I18nKey.Ui.NewsPosts.Save]</button>
                            @if (feed.Id != Guid.Empty) {
                                <button type="button" class="btn btn-outline-danger btn-sm" @onclick="() => DeleteFeedAsync(feed)">@I18n[I18nKey.Ui.NewsPosts.Delete]</button>
                            }
                        </td>
                    </tr>
                }
            </tbody>
        </table>
        <button type="button" class="btn btn-outline-secondary btn-sm" @onclick="AddFeedRow">@I18n[I18nKey.Ui.NewsPosts.FeedAdd]</button>
    </div>

    <h5>@I18n[I18nKey.Ui.NewsPosts.SourcesTitle]</h5>
    <div class="table-responsive">
        <table class="table align-middle">
            <thead>
                <tr>
                    <th>@I18n[I18nKey.Ui.NewsPosts.SourceTitle]</th>
                    <th>@I18n[I18nKey.Ui.NewsPosts.SourceSections]</th>
                    <th>@I18n[I18nKey.Ui.NewsPosts.SourceLastSync]</th>
                    <th></th>
                </tr>
            </thead>
            <tbody>
                @foreach (var source in sources) {
                    <tr>
                        <td>@source.WikiPageTitle</td>
                        <td>@source.SectionCount</td>
                        <td class="small">
                            @if (source.LastSyncedAt != null) {
                                <LocalTime Value="source.LastSyncedAt.Value" Format="dd.MM.yyyy HH:mm" />
                            }
                            @if (!string.IsNullOrEmpty(source.LastError)) {
                                <div class="text-danger">@source.LastError</div>
                            }
                        </td>
                        <td class="text-end">
                            <button type="button" class="btn btn-outline-danger btn-sm" @onclick="() => DeleteSourceAsync(source)">@I18n[I18nKey.Ui.NewsPosts.Delete]</button>
                        </td>
                    </tr>
                }
            </tbody>
        </table>
    </div>
    <div class="d-flex gap-2 align-items-start flex-wrap">
        <div>
            <input class="form-control form-control-sm" placeholder="Positionspapiere/" @bind="newSourceTitle" />
            <div class="form-text">@I18n[I18nKey.Ui.NewsPosts.SourceHint]</div>
        </div>
        <button type="button" class="btn btn-outline-secondary btn-sm" @onclick="AddSourceAsync">@I18n[I18nKey.Ui.NewsPosts.SourceAdd]</button>
        <button type="button" class="btn btn-primary btn-sm" disabled="@syncing" @onclick="SyncAsync">
            @if (syncing) {
                <span class="spinner-border spinner-border-sm me-1"></span>
            }
            @I18n[I18nKey.Ui.NewsPosts.SyncNow]
        </button>
    </div>
</RequirePermission>
```

`NewsPostsSettings.razor.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.NewsPosts;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class NewsPostsSettings {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }

    private List<NewsFeedDTO> feeds = [];
    private List<ProgrammeSourceDTO> sources = [];
    private NewsPostsStatusDTO? status;
    private string newSourceTitle = "";
    private bool syncing;

    protected override async Task OnInitializedAsync() {
        await LoadAsync();
    }

    private async Task LoadAsync() {
        try {
            feeds = await Http.GetFromJsonAsync<List<NewsFeedDTO>>("/api/newsposts/feeds") ?? [];
            sources = await Http.GetFromJsonAsync<List<ProgrammeSourceDTO>>("/api/newsposts/sources") ?? [];
            status = await Http.GetFromJsonAsync<NewsPostsStatusDTO>("/api/newsposts/status");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private void AddFeedRow() {
        feeds.Add(new NewsFeedDTO { IsEnabled = true });
    }

    private async Task SaveFeedAsync(NewsFeedDTO feed) {
        var response = await Http.PutAsJsonAsync("/api/newsposts/feeds", feed);
        if (!response.IsSuccessStatusCode) {
            await ToastService.ErrorAsync(response);
            return;
        }
        await LoadAsync();
    }

    private async Task DeleteFeedAsync(NewsFeedDTO feed) {
        var response = await Http.DeleteAsync($"/api/newsposts/feeds/{feed.Id}");
        if (!response.IsSuccessStatusCode)
            await ToastService.ErrorAsync(response);
        await LoadAsync();
    }

    private async Task AddSourceAsync() {
        var response = await Http.PostAsJsonAsync("/api/newsposts/sources", new ProgrammeSourceCreateRequest { WikiPageTitle = newSourceTitle });
        if (!response.IsSuccessStatusCode) {
            await ToastService.ErrorAsync(response);
            return;
        }
        newSourceTitle = "";
        await LoadAsync();
    }

    private async Task DeleteSourceAsync(ProgrammeSourceDTO source) {
        var response = await Http.DeleteAsync($"/api/newsposts/sources/{source.Id}");
        if (!response.IsSuccessStatusCode)
            await ToastService.ErrorAsync(response);
        await LoadAsync();
    }

    private async Task SyncAsync() {
        syncing = true;
        try {
            var response = await Http.PostAsync("/api/newsposts/sources/sync", null);
            if (response.IsSuccessStatusCode) {
                ToastService.ToastKey(I18nKey.Ui.NewsPosts.SyncDone);
            } else {
                await ToastService.ErrorAsync(response);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        syncing = false;
        await LoadAsync();
    }

    private string YesNo(bool value) => value ? I18n[I18nKey.Ui.NewsPosts.Yes] : I18n[I18nKey.Ui.NewsPosts.No];
}
```

(Check `PageBackLink`'s parameter name in `Components/PageBackLink.razor.cs` and adjust `Href` if it differs. The Blazor `HttpClient` default timeout is 100 s; if the first full sync exceeds it, the toast shows an error while the server keeps syncing. Acceptable for V1; note it in the report if observed.)

- [ ] **Step 5: Changelog**

Append to `Quartermaster.Server/Changelog/V3.md`:

```markdown

### Social Media

Unter "Vorstandsarbeit > Social Media" erscheinen Vorschläge für kurze Beiträge zu aktuellen Nachrichten, abgeleitet aus unserem Programm. Ein Vorschlag lässt sich bearbeiten, auf Kanäle verteilen und sofort oder zeitgesteuert veröffentlichen.
```

- [ ] **Step 6: Build, restart, smoke check**

Run: `dotnet build Quartermaster.sln -m:2 -v:q -nologo`. Restart the local server (kill the `Quartermaster.Server` process by exact name, then `nohup dotnet run --no-build --launch-profile https` from `Quartermaster.Server`). With curl and the admin token, check `GET /api/newsposts/status` returns 200 and `GET /api/newsposts/feeds` lists the three seeded feeds. Leave the visual check to the user.

- [ ] **Step 7: Self-review against `CLAUDE.md`** over the whole diff (usings vs. qualified names, braces, access modifiers, no `@code`, comments), then Checkpoint (user commits).

---

## Manual end-to-end check (user, before enabling in production)

1. Set `newsposts.mistral.api_key`, `newsposts.postiz.base_url` (for example `https://<postiz>/api`), `newsposts.postiz.api_key`.
2. Settings page: "Jetzt synchronisieren"; expect several hundred sections and no source errors.
3. Set `newsposts.enabled` to `true`; after one interval the queue shows suggestions. Tune `newsposts.triage.min_similarity` if nearly everything or nothing passes.
4. Greenlight one candidate to a test channel with "Geplant" a few minutes ahead; confirm it appears scheduled in Postiz, then delete it there.
