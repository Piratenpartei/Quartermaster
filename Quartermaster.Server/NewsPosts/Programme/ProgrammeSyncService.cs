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

    private static readonly SemaphoreSlim SyncLock = new(1, 1);

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

    /// <summary>Waits for any sync already in progress (elsewhere in the process) to finish, then runs. Used by
    /// the weekly hosted service, which has no one waiting on a response.</summary>
    public async Task SyncAllAsync(CancellationToken ct) {
        await SyncLock.WaitAsync(ct);
        try {
            await RunSyncAsync(ct);
        } finally {
            SyncLock.Release();
        }
    }

    /// <summary>Non-blocking variant for the manual sync endpoint: returns false immediately when a sync is
    /// already running elsewhere, instead of queuing behind it.</summary>
    public async Task<bool> TrySyncAllAsync(CancellationToken ct) {
        if (!await SyncLock.WaitAsync(0, ct))
            return false;
        try {
            await RunSyncAsync(ct);
            return true;
        } finally {
            SyncLock.Release();
        }
    }

    private async Task RunSyncAsync(CancellationToken ct) {
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
        var existingRows = _repo.GetSections(sourceId, page.Title);

        // A page can already hold duplicate HeadingPath rows from before this healing existed
        // (e.g. from a race between overlapping syncs); keep the oldest row per path for the
        // diff below and delete the rest, instead of letting ToDictionary throw on the page forever.
        var duplicateIds = existingRows
            .GroupBy(s => s.HeadingPath)
            .Where(g => g.Count() > 1)
            .SelectMany(g => g.OrderBy(s => s.Id).Skip(1).Select(s => s.Id))
            .ToList();
        var existing = existingRows
            .Where(s => !duplicateIds.Contains(s.Id))
            .ToDictionary(s => s.HeadingPath);
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

        var removedIds = existing.Values.Where(s => !keptIds.Contains(s.Id)).Select(s => s.Id).Concat(duplicateIds).ToList();
        _repo.ApplyPageDiff(added, changed, removedIds);
    }

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
