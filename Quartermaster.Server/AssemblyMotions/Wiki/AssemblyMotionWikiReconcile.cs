using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.AssemblyMotions.Wiki;

/// <summary>Detects wiki pages that were deleted or edited by hand and queues the affected motions for a new sync.</summary>
public class AssemblyMotionWikiReconcile {
    public const int MaxPagesPerRun = 500;
    private const int MaxConsecutiveErrors = 3;

    private readonly AssemblyMotionRepository _motions;
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly IWikiBotClient _wiki;
    private readonly OptionRepository _options;
    private readonly ILogger<AssemblyMotionWikiReconcile> _logger;

    public AssemblyMotionWikiReconcile(AssemblyMotionRepository motions, AssemblyMotionPortalRepository portals,
        IWikiBotClient wiki, OptionRepository options, ILogger<AssemblyMotionWikiReconcile> logger) {
        _motions = motions;
        _portals = portals;
        _wiki = wiki;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Compares one daily rotating window of at most <paramref name="batchSize"/> (default <see cref="MaxPagesPerRun"/>) synced pages with the stored hash and marks mismatching or missing pages pending. Returns the number of motions queued. Stops early after repeated wiki errors. Does nothing when the wiki settings are not configured.
    /// </summary>
    public async Task<int> RunAsync(DateTime nowUtc, CancellationToken ct, int batchSize = MaxPagesPerRun) {
        var settings = WikiBotSettings.ReadFrom(_options);
        if (!settings.IsConfigured())
            return 0;

        var checkedCount = 0;
        var queued = 0;
        var skipped = 0;
        var consecutiveErrors = 0;
        foreach (var motion in _motions.ListWikiSynced(nowUtc, batchSize)) {
            ct.ThrowIfCancellationRequested();
            try {
                var portal = _portals.Get(motion.PortalId);
                var type = _portals.GetType(motion.TypeId);
                if (portal == null || type == null || string.IsNullOrWhiteSpace(portal.WikiPagePrefix)) {
                    _logger.LogWarning("Wiki reconcile skipped assembly motion {MotionId}: portal, type or page prefix is missing", motion.Id);
                    skipped++;
                    continue;
                }

                var content = await _wiki.GetContentAsync(settings, AssemblyMotionWikiPage.Title(portal, type, motion), ct);
                checkedCount++;
                consecutiveErrors = 0;
                if (content != null && WikiPageText.Hash(content) == motion.WikiSyncedHash)
                    continue;

                if (content == null) {
                    _motions.MarkWikiPendingAsNeverSynced(motion.Id, nowUtc);
                } else {
                    _motions.MarkWikiPending(motion.Id, nowUtc);
                }
                queued++;
            } catch (Exception ex) when (!ct.IsCancellationRequested) {
                _logger.LogWarning(ex, "Wiki reconcile of assembly motion {MotionId} failed", motion.Id);
                consecutiveErrors++;
                if (consecutiveErrors >= MaxConsecutiveErrors)
                    break;
            }
        }

        _logger.LogInformation("Wiki reconcile checked {Checked} pages, queued {Queued} motions, skipped {Skipped}", checkedCount, queued, skipped);
        return queued;
    }
}
