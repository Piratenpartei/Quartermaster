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

    private NewsItem Seed(string title, NewsItemStatus status = NewsItemStatus.New, DateTime? publishedAt = null) {
        var item = new NewsItem {
            FeedId = _feed.Id, ExternalId = title, Url = "https://x/" + title, Title = title, Teaser = "",
            PublishedAt = publishedAt ?? Now, FetchedAt = Now, UpdatedAt = Now, Status = status
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
        var second = Seed("Alterskontrolle B", publishedAt: Now.AddSeconds(1));
        _mistral.ChatAnswers.Enqueue("kein json");
        _mistral.ChatAnswers.Enqueue("""{"relevant": false, "reason": "nein"}""");

        await _pipeline.RunOnceAsync(Now, CancellationToken.None);

        await Assert.That(_itemRepo.Get(first.Id)!.FailureCount).IsEqualTo(1);
        await Assert.That(_itemRepo.Get(second.Id)!.Status).IsEqualTo(NewsItemStatus.Irrelevant);
    }

    [Test]
    public async Task Generation_failure_after_triage_counts_one_failure_and_item_stays_relevant() {
        var item = Seed("Alterskontrolle Generierung");
        _mistral.ChatAnswers.Enqueue("""{"relevant": true, "reason": "Jugendschutz"}""");
        _mistral.ChatAnswers.Enqueue("kein json");

        await _pipeline.RunOnceAsync(Now, CancellationToken.None);

        var stored = _itemRepo.Get(item.Id)!;
        await Assert.That(stored.FailureCount).IsEqualTo(1);
        await Assert.That(stored.Status).IsEqualTo(NewsItemStatus.Relevant);
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
    public async Task Item_posted_while_generation_is_in_flight_keeps_its_status_and_candidates() {
        var item = Seed("Alterskontrolle während Generierung", NewsItemStatus.Relevant);
        item.MatchedSectionIds = System.Text.Json.JsonSerializer.Serialize(new[] { _youthSection.Id });
        _itemRepo.Update(item);
        _itemRepo.ReplaceCandidates(item.Id, [new PostCandidate { Text = "Alter Vorschlag", SectionId = _youthSection.Id }]);
        var originalCandidateId = _itemRepo.GetCandidates(item.Id).Single().Id;
        _mistral.ChatAnswers.Enqueue($$"""{"candidates":[{"text":"Neuer Vorschlag","sectionId":"{{_youthSection.Id}}"}]}""");
        _mistral.OnBeforeChatAnswer = () => _itemRepo.SetStatus(item.Id, NewsItemStatus.Posted);

        await _pipeline.RunOnceAsync(Now, CancellationToken.None);

        var stored = _itemRepo.Get(item.Id)!;
        await Assert.That(stored.Status).IsEqualTo(NewsItemStatus.Posted);
        var candidates = _itemRepo.GetCandidates(item.Id);
        await Assert.That(candidates.Single().Id).IsEqualTo(originalCandidateId);
        await Assert.That(candidates.Single().Text).IsEqualTo("Alter Vorschlag");
    }

    [Test]
    public async Task Dismissed_during_triage_is_not_revived_by_the_run() {
        var item = Seed("Alterskontrolle wird verworfen");
        _mistral.OnBeforeChatAnswer = () => _itemRepo.SetStatus(item.Id, NewsItemStatus.Dismissed);
        _mistral.ChatAnswers.Enqueue("""{"relevant": true, "reason": "Jugendschutz"}""");

        await _pipeline.RunOnceAsync(Now, CancellationToken.None);

        var stored = _itemRepo.Get(item.Id)!;
        await Assert.That(stored.Status).IsEqualTo(NewsItemStatus.Dismissed);
        await Assert.That(_itemRepo.GetCandidates(item.Id)).IsEmpty();
    }

    [Test]
    public async Task Timeout_like_failure_counts_as_a_failure_and_continues_with_next_item() {
        var first = Seed("Alterskontrolle A");
        var second = Seed("Alterskontrolle B", publishedAt: Now.AddSeconds(1));
        _mistral.ChatExceptions.Enqueue(new TaskCanceledException("timeout"));
        _mistral.ChatAnswers.Enqueue("""{"relevant": false, "reason": "nein"}""");

        await _pipeline.RunOnceAsync(Now, CancellationToken.None);

        await Assert.That(_itemRepo.Get(first.Id)!.FailureCount).IsEqualTo(1);
        await Assert.That(_itemRepo.Get(second.Id)!.Status).IsEqualTo(NewsItemStatus.Irrelevant);
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
