using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Data;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.Events;
using Quartermaster.Data.Options;
using Quartermaster.Server.AssemblyMotions.Wiki;
using Quartermaster.Server.Notifications;

namespace Quartermaster.Server.AssemblyMotions.Forum;

/// <summary>
/// Mirrors numbered assembly motions to Discourse topics. Like the wiki sync it never holds a transaction or row lock across a forum call: it reads, talks to the forum, then records the outcome with a conditional update that cannot overwrite a concurrent change.
/// </summary>
public class AssemblyMotionForumSync {
    public const int DueBatchSize = 10;
    public const string CategoryMissingCode = "category_missing";
    public const string BaseUrlMissingCode = "base_url_missing";
    public const string TopicUnreachableCode = "topic_unreachable";
    public const string DeletedMessage = "Das Thema wurde im Forum gelöscht.";

    private const int AuditErrorMaxLength = 1000;

    private static readonly TimeSpan MaxBackoff = TimeSpan.FromHours(6);
    private static readonly TimeSpan UnconfiguredWarningInterval = TimeSpan.FromHours(1);
    private static readonly HashSet<string> PermanentCodes = [
        DiscourseException.Unauthorized,
        DiscourseException.InsecureUrl,
        CategoryMissingCode,
        BaseUrlMissingCode,
        TopicUnreachableCode
    ];
    private static readonly Lock WarningLock = new();
    private static DateTime lastUnconfiguredWarning = DateTime.MinValue;

    private readonly AssemblyMotionRepository _motions;
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly EventRepository _events;
    private readonly DbContext _db;
    private readonly IDiscourseClient _forum;
    private readonly OptionRepository _options;
    private readonly AuditLogRepository _auditLog;
    private readonly I18nService _i18n;
    private readonly ILogger<AssemblyMotionForumSync> _logger;

    public AssemblyMotionForumSync(AssemblyMotionRepository motions, AssemblyMotionPortalRepository portals, EventRepository events,
        DbContext db, IDiscourseClient forum, OptionRepository options, AuditLogRepository auditLog, I18nService i18n,
        ILogger<AssemblyMotionForumSync> logger) {
        _motions = motions;
        _portals = portals;
        _events = events;
        _db = db;
        _forum = forum;
        _options = options;
        _auditLog = auditLog;
        _i18n = i18n;
        _logger = logger;
    }

    /// <summary>Test seam: forgets when the unconfigured-settings warning was last logged.</summary>
    internal static void ResetUnconfiguredWarning() {
        using (WarningLock.EnterScope()) {
            lastUnconfiguredWarning = DateTime.MinValue;
        }
    }

    /// <summary>
    /// Syncs the motions that are due and returns how many topics now match their motion. Does nothing when the settings are not configured or the forum rejects the account;
    /// a rate limit ends the run without recording anything, so the motions stay due.
    /// </summary>
    public async Task<int> SyncDueAsync(DateTime nowUtc, CancellationToken ct) {
        var settings = DiscourseSettings.ReadFrom(_options);
        if (!settings.IsConfigured()) {
            WarnUnconfigured(nowUtc);
            return 0;
        }

        var due = _motions.ListForumDue(nowUtc, DueBatchSize);
        if (due.Count == 0)
            return 0;

        // A wrong base URL or a revoked key would otherwise mark every motion failed, or deleted after a 404.
        try {
            await _forum.GetCurrentUserAsync(settings, ct);
        } catch (Exception ex) when (!IsShutdown(ex, ct)) {
            _logger.LogWarning(ex, "Forum sync skipped: the session check against the forum failed");
            return 0;
        }

        var synced = 0;
        foreach (var motion in due) {
            ct.ThrowIfCancellationRequested();
            try {
                if (await AttemptAsync(settings, motion.Id, nowUtc, ct))
                    synced++;
            } catch (DiscourseException ex) when (ex.Code == DiscourseException.RateLimited) {
                _logger.LogWarning("Forum sync stops for this run: the forum rate-limits requests (retry after {RetryAfter})", ex.RetryAfter);
                break;
            } catch (Exception ex) when (!ct.IsCancellationRequested) {
                _logger.LogError(ex, "Forum sync of assembly motion {MotionId} failed unexpectedly", motion.Id);
            }
        }

        return synced;
    }

    private async Task<bool> AttemptAsync(DiscourseSettings settings, Guid motionId, DateTime nowUtc, CancellationToken ct) {
        var motion = _motions.Get(motionId);
        if (motion == null || !motion.WasSubmitted || motion.ForumSyncState == ForumSyncState.Deleted)
            return false;

        var portal = _portals.Get(motion.PortalId);
        if (portal == null || !portal.ForumMirrorEnabled)
            return false;

        var type = _portals.GetType(motion.TypeId);
        if (type == null)
            return false;

        var requestedAt = motion.ForumSyncRequestedAt;
        try {
            var categoryId = portal.ForumCategoryId
                ?? throw new DiscourseException("Für das Portal ist keine Forum-Kategorie eingestellt.", CategoryMissingCode);
            var input = BuildInput(portal, type, motion);
            var title = AssemblyMotionForumPost.Title(input);
            var raw = AssemblyMotionForumPost.Raw(input, settings.MaxPostLength);

            var hash = await WriteAsync(settings, motion, categoryId, input, title, raw, ct);
            if (hash == null) {
                _motions.MarkForumDeleted(motion.Id, DeletedMessage);
                _auditLog.Log("AssemblyMotion", motion.Id, "ForumTopicDeleted");
                return false;
            }

            _motions.CompleteForumSync(motion.Id, requestedAt, motion.Revision, hash, nowUtc);
            return true;
        } catch (Exception ex) when (IsRecordable(ex, ct)) {
            RecordFailure(motion, requestedAt, ex, nowUtc);
            return false;
        }
    }

    /// <returns>The hash of what the topic now holds, or null when the topic was deleted in the forum.</returns>
    private async Task<string?> WriteAsync(DiscourseSettings settings, AssemblyMotion motion, int categoryId, AssemblyMotionForumInput input,
        string title, string raw, CancellationToken ct) {
        var topicId = motion.ForumTopicId;
        var postId = motion.ForumPostId;
        var takenOver = false;
        if (topicId == null || postId == null) {
            var found = await _forum.FindTopicByExternalIdAsync(settings, ExternalId(motion), ct);
            if (found == null)
                return await CreateAsync(settings, motion, categoryId, input, title, raw, ct);

            _motions.SetForumTopic(motion.Id, found.TopicId, found.FirstPostId);
            topicId = found.TopicId;
            postId = found.FirstPostId;
            takenOver = true;
        }

        if (!takenOver && motion.ForumSyncedHash == AssemblyMotionForumPost.Hash(title, raw))
            return motion.ForumSyncedHash;

        DiscourseTopic topic;
        DiscoursePost post;
        try {
            topic = await _forum.GetTopicAsync(settings, topicId.Value, ct);
            post = await _forum.GetPostAsync(settings, postId.Value, ct);
        } catch (DiscourseException ex) when (ex.Code == DiscourseException.NotFound) {
            // Discourse answers 404 both for a hard-deleted topic and for one the bot can no longer see (category permissions, moved topic),
            // so only deleted_at, which a moderator still reads on a soft-deleted topic, marks the motion as deleted.
            throw new DiscourseException("Das Forum-Thema ist nicht erreichbar: Es wurde endgültig gelöscht oder das Bot-Konto kann es nicht mehr sehen.", TopicUnreachableCode, 404, inner: ex);
        }
        if (topic.Deleted || post.Deleted)
            return null;

        var current = WikiPageText.Normalize(post.Raw);
        if (motion.ForumSyncedHash != null && AssemblyMotionForumPost.Hash(topic.Title, current) != motion.ForumSyncedHash)
            _auditLog.Log("AssemblyMotion", motion.Id, "ForumContentDiffered", "ForumTopicId", null, topicId.Value.ToString());

        var written = raw;
        if (current != raw)
            written = await EditAsync(settings, motion, postId.Value, input, raw, ct);
        if (topic.Title != title)
            await _forum.UpdateTopicTitleAsync(settings, topicId.Value, title, ct);

        return AssemblyMotionForumPost.Hash(title, written);
    }

    private async Task<string> CreateAsync(DiscourseSettings settings, AssemblyMotion motion, int categoryId, AssemblyMotionForumInput input,
        string title, string raw, CancellationToken ct) {
        DiscourseTopic created;
        var written = raw;
        try {
            created = await _forum.CreateTopicAsync(settings, title, raw, categoryId, ExternalId(motion), ct);
        } catch (DiscourseException ex) when (ex.Code == DiscourseException.Rejected && ShortVersion(input, raw) != null) {
            written = ShortVersion(input, raw)!;
            created = await _forum.CreateTopicAsync(settings, title, written, categoryId, ExternalId(motion), ct);
        }

        _motions.SetForumTopic(motion.Id, created.TopicId, created.FirstPostId);
        return AssemblyMotionForumPost.Hash(title, written);
    }

    private async Task<string> EditAsync(DiscourseSettings settings, AssemblyMotion motion, int postId, AssemblyMotionForumInput input, string raw, CancellationToken ct) {
        var reason = EditReason(motion);
        try {
            await _forum.EditPostAsync(settings, postId, raw, reason, ct);
            return raw;
        } catch (DiscourseException ex) when (ex.Code == DiscourseException.Rejected && ShortVersion(input, raw) != null) {
            var shortVersion = ShortVersion(input, raw)!;
            await _forum.EditPostAsync(settings, postId, shortVersion, reason, ct);
            return shortVersion;
        }
    }

    /// <summary>The post with both texts replaced by notices, or null when <paramref name="raw"/> already is that version.</summary>
    private static string? ShortVersion(AssemblyMotionForumInput input, string raw) {
        var shortVersion = AssemblyMotionForumPost.Raw(input, int.MaxValue, forceNotices: true);
        return shortVersion == raw ? null : shortVersion;
    }

    private static string ExternalId(AssemblyMotion motion) {
        return $"qm-motion-{motion.Id:D}";
    }

    private static string EditReason(AssemblyMotion motion) {
        return motion.ForumSyncedRevision != motion.Revision
            ? $"Antragsportal: Revision {motion.Revision} eingereicht"
            : $"Antragsportal: Status aktualisiert (Revision {motion.Revision})";
    }

    private AssemblyMotionForumInput BuildInput(AssemblyMotionPortal portal, AssemblyMotionType type, AssemblyMotion motion) {
        var names = AssemblyMotionSubmitterNames.ForMotions(_db, _motions, [motion]);
        return new AssemblyMotionForumInput(
            motion,
            type,
            _events.Get(portal.EventId)?.PublicName ?? "",
            names.GetValueOrDefault(motion.Id, []),
            StatusLabel(motion),
            ResultLabel(motion.Result),
            PortalUrl(motion),
            WikiUrl(portal, type, motion));
    }

    private string StatusLabel(AssemblyMotion motion) {
        if (motion.IsInRevision)
            return _i18n[I18nKey.Ui.AssemblyMotion.StatusInRevision];

        return motion.Status switch {
            AssemblyMotionStatus.CollectingCoSubmitters => _i18n[I18nKey.Ui.AssemblyMotion.StatusCollecting],
            AssemblyMotionStatus.Submitted => _i18n[I18nKey.Ui.AssemblyMotion.StatusSubmitted],
            AssemblyMotionStatus.Admitted => _i18n[I18nKey.Ui.AssemblyMotion.StatusAdmitted],
            AssemblyMotionStatus.FormallyInsufficient => _i18n[I18nKey.Ui.AssemblyMotion.StatusFormallyInsufficient],
            AssemblyMotionStatus.Withdrawn => _i18n[I18nKey.Ui.AssemblyMotion.StatusWithdrawn],
            AssemblyMotionStatus.Lapsed => _i18n[I18nKey.Ui.AssemblyMotion.StatusLapsed],
            _ => _i18n[I18nKey.Ui.AssemblyMotion.StatusDraft]
        };
    }

    private string? ResultLabel(AssemblyMotionResult result) {
        return result switch {
            AssemblyMotionResult.Accepted => _i18n[I18nKey.Ui.AssemblyMotion.ResultAccepted],
            AssemblyMotionResult.AcceptedModified => _i18n[I18nKey.Ui.AssemblyMotion.ResultAcceptedModified],
            AssemblyMotionResult.Rejected => _i18n[I18nKey.Ui.AssemblyMotion.ResultRejected],
            AssemblyMotionResult.NotHandled => _i18n[I18nKey.Ui.AssemblyMotion.ResultNotHandled],
            _ => null
        };
    }

    private string PortalUrl(AssemblyMotion motion) {
        var baseUrl = _options.GetGlobalValue(NotificationTemplateGlobals.BaseUrlOptionKey)?.Value?.Trim().TrimEnd('/') ?? "";
        if (baseUrl.Length == 0)
            throw new DiscourseException("Die öffentliche Basis-URL (system.public_base_url) ist nicht gesetzt.", BaseUrlMissingCode);

        return $"{baseUrl}/AssemblyMotions/{motion.Id}";
    }

    private string? WikiUrl(AssemblyMotionPortal portal, AssemblyMotionType type, AssemblyMotion motion) {
        if (!portal.WikiMirrorEnabled || string.IsNullOrWhiteSpace(portal.WikiPagePrefix))
            return null;

        return AssemblyMotionWikiPage.Url(WikiBotSettings.ReadFrom(_options).ApiUrl, AssemblyMotionWikiPage.Title(portal, type, motion));
    }

    private void RecordFailure(AssemblyMotion motion, DateTime? requestedAt, Exception exception, DateTime nowUtc) {
        var newCount = motion.ForumFailureCount + 1;
        var delay = IsPermanent(exception) ? MaxBackoff : AssemblyMotionWikiSync.BackoffFor(motion.ForumFailureCount);
        var message = FailureMessage(exception);
        _motions.FailForumSync(motion.Id, requestedAt, message, newCount, nowUtc + delay);

        if (exception is DiscourseException || exception is HttpRequestException || exception is TimeoutException) {
            _logger.LogWarning(exception, "Forum sync of assembly motion {MotionId} failed (attempt {Count})", motion.Id, newCount);
        } else {
            _logger.LogError(exception, "Forum sync of assembly motion {MotionId} failed unexpectedly (attempt {Count})", motion.Id, newCount);
        }

        if (newCount == 1)
            _auditLog.Log("AssemblyMotion", motion.Id, "ForumSyncFailed", "ForumLastError", null, Truncate(message, AuditErrorMaxLength));
    }

    private static bool IsPermanent(Exception exception) {
        return exception is DiscourseException forumException && PermanentCodes.Contains(forumException.Code);
    }

    private static bool IsRecordable(Exception exception, CancellationToken ct) {
        if (IsShutdown(exception, ct))
            return false;

        return exception is not DiscourseException forumException || forumException.Code != DiscourseException.RateLimited;
    }

    private static bool IsShutdown(Exception exception, CancellationToken ct) {
        return exception is OperationCanceledException && ct.IsCancellationRequested;
    }

    private static string FailureMessage(Exception exception) {
        if (exception is DiscourseException)
            return exception.Message;
        return "Interner Fehler beim Forum-Abgleich, Details stehen im Serverprotokoll.";
    }

    private static string Truncate(string text, int maxLength) {
        return text.Length > maxLength ? text[..maxLength] : text;
    }

    private void WarnUnconfigured(DateTime nowUtc) {
        using (WarningLock.EnterScope()) {
            if (nowUtc - lastUnconfiguredWarning < UnconfiguredWarningInterval)
                return;
            lastUnconfiguredWarning = nowUtc;
        }

        _logger.LogWarning("Forum mirroring is skipped because the Discourse settings are not configured");
    }
}
