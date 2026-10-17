using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.Options;
using Quartermaster.Server.Notifications;

namespace Quartermaster.Server.AssemblyMotions.Wiki;

/// <summary>
/// Mirrors numbered assembly motions to wiki pages. Never holds a transaction or row lock across a wiki call: it reads, talks to the wiki, then records the outcome with a conditional update that cannot overwrite a concurrent change.
/// </summary>
public class AssemblyMotionWikiSync {
    public const string LoginFailedCode = "login_failed";
    public const string TemplateErrorCode = "template_error";
    public const string BaseUrlMissingCode = "base_url_missing";
    public const string PagePrefixMissingCode = "page_prefix_missing";
    public const string ForeignPageCode = "foreign_page";
    public const string InternalErrorCode = "internal_error";
    public const string ForeignPageMessage = "Im Wiki existiert unter diesem Titel bereits eine Seite, die nicht vom Antragsportal angelegt wurde. Bitte Seitenpräfix prüfen oder die Wiki-Seite verschieben.";

    private const int DueBatchSize = 20;
    private const int MaxBackoffExponent = 20;
    private const int AuditErrorMaxLength = 1000;

    private static readonly TimeSpan MaxBackoff = TimeSpan.FromHours(6);
    private static readonly TimeSpan ProtectionRetryDelay = TimeSpan.FromHours(1);
    private static readonly TimeSpan UnconfiguredWarningInterval = TimeSpan.FromHours(1);
    private static readonly HashSet<string> PermanentCodes = [
        LoginFailedCode,
        "permissiondenied",
        "protectedpage",
        "insecure_url",
        TemplateErrorCode,
        BaseUrlMissingCode,
        PagePrefixMissingCode,
        ForeignPageCode
    ];
    private static readonly Lock WarningLock = new();
    private static DateTime lastUnconfiguredWarning = DateTime.MinValue;

    private readonly AssemblyMotionRepository _motions;
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly AssemblyMotionInquiryRepository _inquiries;
    private readonly AssemblyMotionCompetitionRepository _competitions;
    private readonly IWikiBotClient _wiki;
    private readonly OptionRepository _options;
    private readonly AuditLogRepository _auditLog;
    private readonly ILogger<AssemblyMotionWikiSync> _logger;

    public AssemblyMotionWikiSync(AssemblyMotionRepository motions, AssemblyMotionPortalRepository portals,
        AssemblyMotionInquiryRepository inquiries, AssemblyMotionCompetitionRepository competitions,
        IWikiBotClient wiki, OptionRepository options, AuditLogRepository auditLog, ILogger<AssemblyMotionWikiSync> logger) {
        _motions = motions;
        _portals = portals;
        _inquiries = inquiries;
        _competitions = competitions;
        _wiki = wiki;
        _options = options;
        _auditLog = auditLog;
        _logger = logger;
    }

    /// <summary>Test seam: forgets when the unconfigured-settings warning was last logged.</summary>
    internal static void ResetUnconfiguredWarning() {
        using (WarningLock.EnterScope()) {
            lastUnconfiguredWarning = DateTime.MinValue;
        }
    }

    /// <summary>Syncs the motions that are due and returns how many were written or confirmed unchanged. Does nothing when the wiki settings are not configured.</summary>
    public async Task<int> SyncDueAsync(DateTime nowUtc, CancellationToken ct) {
        if (!WikiBotSettings.ReadFrom(_options).IsConfigured()) {
            WarnUnconfigured(nowUtc);
            return 0;
        }

        var synced = 0;
        foreach (var due in _motions.ListWikiDue(nowUtc, DueBatchSize)) {
            ct.ThrowIfCancellationRequested();
            try {
                var attempt = await AttemptAsync(due.Id, nowUtc, ct);
                if (attempt.Written)
                    synced++;
                if (IsLoginFailure(attempt.Failure)) {
                    _logger.LogWarning("Wiki sync stops for this run after a failed login");
                    break;
                }
            } catch (Exception ex) when (!ct.IsCancellationRequested) {
                _logger.LogError(ex, "Wiki sync of assembly motion {MotionId} failed unexpectedly", due.Id);
            }
        }

        return synced;
    }

    /// <summary>
    /// Syncs one motion now. Returns true when the wiki page matches the motion afterwards, false when the motion is not mirrored, the settings are not configured or the attempt failed (recorded on the motion). Shutdown cancellation propagates and records nothing.
    /// </summary>
    public async Task<bool> SyncOneAsync(Guid motionId, DateTime nowUtc, CancellationToken ct) {
        return (await AttemptAsync(motionId, nowUtc, ct)).Written;
    }

    private async Task<SyncAttempt> AttemptAsync(Guid motionId, DateTime nowUtc, CancellationToken ct) {
        var settings = WikiBotSettings.ReadFrom(_options);
        if (!settings.IsConfigured()) {
            WarnUnconfigured(nowUtc);
            return SyncAttempt.NotWritten;
        }

        var motion = _motions.Get(motionId);
        if (motion == null || !motion.WasSubmitted)
            return SyncAttempt.NotWritten;

        var portal = _portals.Get(motion.PortalId);
        if (portal == null || !portal.WikiMirrorEnabled)
            return SyncAttempt.NotWritten;

        var type = _portals.GetType(motion.TypeId);
        if (type == null)
            return SyncAttempt.NotWritten;

        var requestedAt = motion.WikiSyncRequestedAt;
        try {
            var title = PageTitle(portal, type, motion);
            var portalUrl = PortalUrl(motion);
            var text = await RenderAsync(settings, portal, type, motion, portalUrl);
            var hash = WikiPageText.Hash(text);

            var protectionFailure = await WriteAsync(settings, motion, title, text, hash, portalUrl, ct);

            if (IsSuperseded(portal, type, motion, title))
                return SyncAttempt.NotWritten;

            _motions.CompleteWikiSync(
                motion.Id,
                requestedAt,
                protectionFailure == null ? WikiSyncState.Synced : WikiSyncState.ProtectionFailed,
                motion.Revision,
                hash,
                protectionFailure,
                protectionFailure == null ? null : nowUtc + ProtectionRetryDelay,
                nowUtc);
            return new SyncAttempt(true, null);
        } catch (Exception ex) when (!IsShutdown(ex, ct)) {
            RecordFailure(motion, requestedAt, ex, nowUtc);
            return new SyncAttempt(false, ex);
        }
    }

    // A prefix change during the write requeues the motion as never synced; recording the hash of the page written under the old title would undo that.
    private bool IsSuperseded(AssemblyMotionPortal portal, AssemblyMotionType type, AssemblyMotion motion, string writtenTitle) {
        var currentPortal = _portals.Get(portal.Id);
        if (currentPortal == null || !currentPortal.WikiMirrorEnabled)
            return true;

        return PageTitle(currentPortal, type, motion) != writtenTitle;
    }

    private static bool IsLoginFailure(Exception? exception) {
        return exception is WikiBotException wikiException && wikiException.Code == LoginFailedCode;
    }

    private sealed record SyncAttempt(bool Written, Exception? Failure) {
        public static readonly SyncAttempt NotWritten = new(false, null);
    }

    /// <summary>
    /// Delay before the next attempt after a failure: the first failure (previous count 0) waits 1 minute, then 2, 4, ... capped at 6 hours. The exponent is clamped so large counts cannot overflow.
    /// </summary>
    public static TimeSpan BackoffFor(int previousFailureCount) {
        var exponent = Math.Clamp(previousFailureCount, 0, MaxBackoffExponent);
        var delay = TimeSpan.FromMinutes(Math.Pow(2, exponent));
        return delay > MaxBackoff ? MaxBackoff : delay;
    }

    public static bool IsPermanent(Exception exception) {
        if (exception is not WikiBotException wikiException)
            return false;
        if (wikiException.Code == null)
            return false;
        return PermanentCodes.Contains(wikiException.Code);
    }

    private static bool IsShutdown(Exception exception, CancellationToken ct) {
        return exception is OperationCanceledException && ct.IsCancellationRequested;
    }

    private static bool IsWikiFailure(Exception exception, CancellationToken ct) {
        if (exception is WikiBotException || exception is HttpRequestException || exception is TimeoutException)
            return true;

        return exception is OperationCanceledException && !ct.IsCancellationRequested;
    }

    private static string FailureMessage(Exception exception) {
        if (exception is WikiBotException)
            return exception.Message;
        if (exception is HttpRequestException)
            return exception.Message;
        if (exception is TimeoutException)
            return exception.Message;
        if (exception is OperationCanceledException)
            return exception.Message;
        return $"Interner Fehler beim Wiki-Abgleich ({InternalErrorCode}), Details stehen im Serverprotokoll.";
    }

    private static string PageTitle(AssemblyMotionPortal portal, AssemblyMotionType type, AssemblyMotion motion) {
        if (string.IsNullOrWhiteSpace(portal.WikiPagePrefix))
            throw new WikiBotException("Für das Portal ist kein Wiki-Seitenpräfix eingestellt.", PagePrefixMissingCode);

        return AssemblyMotionWikiPage.Title(portal, type, motion);
    }

    /// <returns>Null when the page is written and protected, otherwise the protection error (the page itself is written).</returns>
    private async Task<string?> WriteAsync(WikiBotSettings settings, AssemblyMotion motion, string title, string text, string hash, string portalUrl, CancellationToken ct) {
        var current = await _wiki.GetContentAsync(settings, title, ct);
        var needsProtection = NeedsProtection(motion, current);

        if (current == null) {
            await _wiki.EditAsync(settings, title, text, $"Antragsportal: Antrag angelegt (Revision {motion.Revision})", ct);
        } else {
            var currentHash = WikiPageText.Hash(current);
            if (currentHash != hash) {
                if (motion.WikiSyncedHash == null && !current.Contains(portalUrl))
                    throw ForeignPage(motion, title);

                await _wiki.EditAsync(settings, title, text, EditSummary(motion), ct);
                if (motion.WikiSyncedHash != null && currentHash != motion.WikiSyncedHash)
                    _auditLog.Log("AssemblyMotion", motion.Id, "WikiContentDiffered", "PageTitle", null, title);
            }
        }

        if (!needsProtection)
            return null;

        try {
            await _wiki.ProtectAsync(settings, title, ct);
            return null;
        } catch (Exception ex) when (IsWikiFailure(ex, ct)) {
            _logger.LogWarning(ex, "Protecting wiki page {Title} of assembly motion {MotionId} failed", title, motion.Id);
            return ex.Message;
        }
    }

    private static bool NeedsProtection(AssemblyMotion motion, string? currentPageText) {
        if (currentPageText == null)
            return true;
        if (motion.WikiSyncState == WikiSyncState.ProtectionFailed)
            return true;

        return motion.WikiSyncedHash == null;
    }

    private WikiBotException ForeignPage(AssemblyMotion motion, string title) {
        if (motion.WikiFailureCount == 0)
            _auditLog.Log("AssemblyMotion", motion.Id, "WikiForeignPageRefused", "PageTitle", null, title);

        return new WikiBotException(ForeignPageMessage, ForeignPageCode);
    }

    private static string EditSummary(AssemblyMotion motion) {
        return motion.WikiSyncedRevision != motion.Revision
                ? $"Antragsportal: Revision {motion.Revision} eingereicht"
                : $"Antragsportal: Status aktualisiert (Revision {motion.Revision})";
    }

    private string PortalUrl(AssemblyMotion motion) {
        var baseUrl = _options.GetGlobalValue(NotificationTemplateGlobals.BaseUrlOptionKey)?.Value?.Trim().TrimEnd('/') ?? "";
        if (baseUrl.Length == 0)
            throw new WikiBotException("Die öffentliche Basis-URL (system.public_base_url) ist nicht gesetzt.", BaseUrlMissingCode);

        return $"{baseUrl}/AssemblyMotions/{motion.Id}";
    }

    private async Task<string> RenderAsync(WikiBotSettings settings, AssemblyMotionPortal portal, AssemblyMotionType type, AssemblyMotion motion, string portalUrl) {
        var input = new AssemblyMotionWikiInput(
            portal,
            type,
            motion,
            _inquiries.HasOpen(motion.Id),
            CompetingDisplayNumbers(motion),
            portalUrl,
            settings.Username);

        try {
            return await AssemblyMotionWikiRenderer.RenderAsync(input);
        } catch (InvalidOperationException ex) {
            throw new WikiBotException(ex.Message, TemplateErrorCode, ex);
        }
    }

    private List<string> CompetingDisplayNumbers(AssemblyMotion motion) {
        var numbers = new List<string>();
        foreach (var link in _competitions.ListForMotion(motion.Id)) {
            var other = _motions.Get(link.MotionAId == motion.Id ? link.MotionBId : link.MotionAId);
            if (other == null || !other.WasSubmitted)
                continue;

            var otherType = _portals.GetType(other.TypeId);
            if (otherType == null)
                continue;

            numbers.Add(AssemblyMotionNumber.Format(otherType.Code, other.Number!.Value));
        }

        return numbers;
    }

    private void RecordFailure(AssemblyMotion motion, DateTime? requestedAt, Exception exception, DateTime nowUtc) {
        var newCount = motion.WikiFailureCount + 1;
        var delay = IsPermanent(exception) ? MaxBackoff : BackoffFor(motion.WikiFailureCount);
        var message = FailureMessage(exception);
        _motions.FailWikiSync(motion.Id, requestedAt, message, newCount, nowUtc + delay);

        if (IsWikiFailure(exception, CancellationToken.None)) {
            _logger.LogWarning(exception, "Wiki sync of assembly motion {MotionId} failed (attempt {Count})", motion.Id, newCount);
        } else {
            _logger.LogError(exception, "Wiki sync of assembly motion {MotionId} failed unexpectedly (attempt {Count})", motion.Id, newCount);
        }

        var foreign = exception is WikiBotException wikiException && wikiException.Code == ForeignPageCode;
        if (newCount == 1 && !foreign)
            _auditLog.Log("AssemblyMotion", motion.Id, "WikiSyncFailed", "WikiLastError", null, Truncate(message, AuditErrorMaxLength));
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

        _logger.LogWarning("Wiki mirroring is skipped because the wiki bot settings are not configured");
    }
}
