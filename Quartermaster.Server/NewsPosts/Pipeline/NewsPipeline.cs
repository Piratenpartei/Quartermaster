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

        // Snapshot the Relevant queue before triage runs, so an item that becomes
        // Relevant via the triage cascade below is not generated a second time here.
        var pendingGeneration = _itemRepo.GetByStatus(NewsItemStatus.Relevant);

        var fresh = _itemRepo.GetByStatus(NewsItemStatus.New);
        if (fresh.Count > 0 && _index.Count > 0)
            await MatchAndTriageAsync(fresh, ct);

        foreach (var item in pendingGeneration) {
            var expectedStatus = item.Status;
            await RunStageAsync(item, expectedStatus, ct, () => GenerateAsync(item, expectedStatus, ct));
        }

        var deleted = _itemRepo.DeleteStale(nowUtc - _settings.Retention);
        if (deleted > 0)
            _logger.LogInformation("News pipeline retention removed {Count} items", deleted);
    }

    public async Task<bool> GenerateForItemAsync(Guid itemId, CancellationToken ct) {
        var item = _itemRepo.Get(itemId);
        if (item == null || item.Status == NewsItemStatus.Posted || item.Status == NewsItemStatus.Posting)
            return false;

        var expectedStatus = item.Status;
        if (ParseIds(item.MatchedSectionIds).Count == 0) {
            var vector = (await _mistral.EmbedAsync([EmbeddingInput(item)], ct))[0];
            ApplyMatches(item, _index.TopMatches(vector, MatchCount));
        }
        await GenerateAsync(item, expectedStatus, ct);
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
                RecordFailure(item, item.Status, ex.Message);
            return;
        }

        for (var i = 0; i < items.Count; i++) {
            var item = items[i];
            var expectedStatus = item.Status;
            ApplyMatches(item, _index.TopMatches(vectors[i], MatchCount));
            if ((item.BestSimilarity ?? 0) < _settings.MinSimilarity) {
                item.Status = NewsItemStatus.Irrelevant;
                item.TriageReason = null;
                item.FailureCount = 0;
                item.LastError = null;
                _itemRepo.UpdateIfStatus(item, expectedStatus);
                continue;
            }
            await RunStageAsync(item, expectedStatus, ct, () => TriageAsync(item, expectedStatus, ct));
        }
    }

    /// <summary>Returns false when the item moved away from <paramref name="expectedStatus"/> before the triage
    /// result could be written, in which case the caller must not cascade into generation either.</summary>
    private async Task<bool> TriageAsync(NewsItem item, NewsItemStatus expectedStatus, CancellationToken ct) {
        var result = await _triage.RunAsync(item, LoadPassages(item), ct);
        item.Status = result.Relevant ? NewsItemStatus.Relevant : NewsItemStatus.Irrelevant;
        item.TriageReason = result.Reason;
        item.FailureCount = 0;
        item.LastError = null;
        if (!_itemRepo.UpdateIfStatus(item, expectedStatus))
            return false;

        if (result.Relevant) {
            var relevantStatus = item.Status;
            await RunStageAsync(item, relevantStatus, ct, () => GenerateAsync(item, relevantStatus, ct));
        }
        return true;
    }

    /// <summary>Writes the new status before replacing candidates, and skips the candidate replacement entirely
    /// when the status write loses the race (item moved to Posting/Posted/Dismissed in the meantime), so a
    /// concurrently greenlit or dismissed item never has its candidates rewritten.</summary>
    private async Task<bool> GenerateAsync(NewsItem item, NewsItemStatus expectedStatus, CancellationToken ct) {
        var candidates = await _generator.GenerateAsync(item, LoadPassages(item), ct);

        item.Status = NewsItemStatus.Generated;
        item.FailureCount = 0;
        item.LastError = null;
        if (!_itemRepo.UpdateIfStatus(item, expectedStatus)) {
            _logger.LogInformation("News item {Id} left status {Expected} before generation finished; discarding candidates", item.Id, expectedStatus);
            return false;
        }

        _itemRepo.ReplaceCandidates(item.Id, candidates
            .Select(c => new PostCandidate { Text = c.Text, SectionId = c.SectionId })
            .ToList());
        return true;
    }

    /// <summary>Runs one stage for one item; a model, network or timeout error is recorded on the item instead of
    /// aborting the run.</summary>
    private async Task RunStageAsync(NewsItem item, NewsItemStatus expectedStatus, CancellationToken ct, Func<Task<bool>> stage) {
        try {
            await stage();
        } catch (MistralException ex) {
            _logger.LogWarning(ex, "News item {Id} stage failed", item.Id);
            RecordFailure(item, expectedStatus, ex.Message);
        } catch (HttpRequestException ex) {
            _logger.LogWarning(ex, "News item {Id} stage failed", item.Id);
            RecordFailure(item, expectedStatus, ex.Message);
        } catch (OperationCanceledException ex) when (!ct.IsCancellationRequested) {
            _logger.LogWarning(ex, "News item {Id} stage timed out", item.Id);
            RecordFailure(item, expectedStatus, "Zeitüberschreitung bei der Anfrage.");
        }
    }

    private void RecordFailure(NewsItem item, NewsItemStatus expectedStatus, string error) {
        item.FailureCount++;
        item.LastError = error;
        if (item.FailureCount >= MaxFailures)
            item.Status = NewsItemStatus.Failed;
        _itemRepo.UpdateIfStatus(item, expectedStatus);
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
