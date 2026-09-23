using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;

namespace Quartermaster.Data.NewsPosts;

public class NewsItemRepository {
    private readonly DbContext _context;

    public NewsItemRepository(DbContext context) {
        _context = context;
    }

    public NewsItem? Get(Guid id)
        => _context.NewsItems.Where(i => i.Id == id).FirstOrDefault();

    public bool Exists(Guid feedId, string externalId)
        => _context.NewsItems.Any(i => i.FeedId == feedId && i.ExternalId == externalId);

    public bool HasItems(Guid feedId)
        => _context.NewsItems.Any(i => i.FeedId == feedId);

    /// <summary>Returns false when the feed already holds an item with the same ExternalId.</summary>
    public bool TryInsert(NewsItem item) {
        if (Exists(item.FeedId, item.ExternalId))
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

    /// <summary>Writes the pipeline-mutable fields of <paramref name="item"/> only if its stored status still equals
    /// <paramref name="expectedStatus"/> (the status it had when the calling stage started). Returns false when the
    /// item moved on in the meantime (e.g. a concurrent greenlight or dismiss), so the caller must not overwrite it
    /// or replace its candidates.</summary>
    public bool UpdateIfStatus(NewsItem item, NewsItemStatus expectedStatus) {
        item.UpdatedAt = DateTime.UtcNow;
        var updated = _context.NewsItems
            .Where(i => i.Id == item.Id && i.Status == expectedStatus)
            .Set(i => i.Status, item.Status)
            .Set(i => i.TriageReason, item.TriageReason)
            .Set(i => i.BestSimilarity, item.BestSimilarity)
            .Set(i => i.MatchedSectionIds, item.MatchedSectionIds)
            .Set(i => i.FailureCount, item.FailureCount)
            .Set(i => i.LastError, item.LastError)
            .Set(i => i.UpdatedAt, item.UpdatedAt)
            .Update();
        return updated == 1;
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
