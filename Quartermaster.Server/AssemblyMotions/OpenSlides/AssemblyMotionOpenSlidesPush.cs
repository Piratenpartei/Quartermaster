using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LinqToDB;
using Microsoft.Extensions.Logging;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Data;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.AssemblyMotions.OpenSlides;

public class AssemblyMotionOpenSlidesPush {
    public const string NotConfiguredMessage = "Die OpenSlides-Zugangsdaten sind nicht hinterlegt.";

    // The text is already final (fixed or capped), so it passes through ForAction unchanged.
    private const string CategoryFailedCode = "action_failed";
    private const string LoginFailedPrefix = "Anmeldung an OpenSlides fehlgeschlagen: ";

    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> PortalGates = new();

    private readonly DbContext _db;
    private readonly AssemblyMotionRepository _motions;
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly AssemblyMotionUpvoteRepository _upvotes;
    private readonly IOpenSlidesClient _client;
    private readonly OptionRepository _options;
    private readonly AuditLogRepository _auditLog;
    private readonly ILogger<AssemblyMotionOpenSlidesPush> _logger;

    public AssemblyMotionOpenSlidesPush(DbContext db, AssemblyMotionRepository motions, AssemblyMotionPortalRepository portals,
        AssemblyMotionUpvoteRepository upvotes, IOpenSlidesClient client, OptionRepository options, AuditLogRepository auditLog,
        ILogger<AssemblyMotionOpenSlidesPush> logger) {
        _db = db;
        _motions = motions;
        _portals = portals;
        _upvotes = upvotes;
        _client = client;
        _options = options;
        _auditLog = auditLog;
        _logger = logger;
    }

    public bool IsConfigured() {
        return OpenSlidesSettings.ReadFrom(_options).IsConfigured();
    }

    /// <summary>Makes exactly one login attempt with the stored settings and creates or changes nothing in OpenSlides. The message contains only fixed texts and the user name.</summary>
    public async Task<AssemblyMotionConnectionCheckDTO> ValidateAsync(CancellationToken ct) {
        var settings = OpenSlidesSettings.ReadFrom(_options);
        if (!settings.IsConfigured())
            return new AssemblyMotionConnectionCheckDTO { Ok = false, Message = NotConfiguredMessage };

        try {
            await _client.LoginAsync(settings, ct);
        } catch (Exception ex) when (!ct.IsCancellationRequested) {
            _logger.LogWarning("OpenSlides connection check failed: {Exception}", ex);
            return new AssemblyMotionConnectionCheckDTO { Ok = false, Message = LoginFailedPrefix + OpenSlidesErrorText.Fixed(ex) };
        }

        return new AssemblyMotionConnectionCheckDTO { Ok = true, Message = $"Verbindung erfolgreich: angemeldet als {settings.Username}." };
    }

    /// <summary>Fails with a 409 key when a push for the same portal is already running or the portal has no OpenSlides meeting id. Caller cancellation stops the run between motions and returns what was done.</summary>
    public async Task<OpenSlidesPushOutcome> PushAsync(AssemblyMotionPortal portal, bool includeSubmitted, DateTime nowUtc, CancellationToken ct) {
        var gate = PortalGates.GetOrAdd(portal.Id, _ => new SemaphoreSlim(1, 1));
        if (!await gate.WaitAsync(0, ct))
            return OpenSlidesPushOutcome.Fail(I18nKey.Error.AssemblyMotion.OpenSlidesPushRunning);

        try {
            // The meeting id is read again under the gate because the caller's portal may predate a settings change.
            var current = _portals.Get(portal.Id) ?? portal;
            if (current.OpenSlidesMeetingId == null)
                return OpenSlidesPushOutcome.Fail(I18nKey.Error.AssemblyMotion.OpenSlidesMeetingMissing);

            // Client calls get no cancellable token: an abandoned create would lose the id and duplicate the motion on the next push.
            return OpenSlidesPushOutcome.Ok(await RunAsync(current, includeSubmitted, nowUtc, ct));
        } finally {
            gate.Release();
        }
    }

    private async Task<OpenSlidesPushResultDTO> RunAsync(AssemblyMotionPortal portal, bool includeSubmitted, DateTime nowUtc, CancellationToken ct) {
        var settings = OpenSlidesSettings.ReadFrom(_options);
        var meetingId = portal.OpenSlidesMeetingId!.Value;
        var types = _portals.GetTypes(portal.Id).ToDictionary(t => t.Id);
        var ordered = SelectInOrder(portal, includeSubmitted, types, nowUtc);
        var submitters = SubmitterNames(ordered);
        var categoryErrors = new Dictionary<Guid, string>();
        var result = new OpenSlidesPushResultDTO();

        foreach (var motion in ordered) {
            if (ct.IsCancellationRequested)
                break;

            var type = types[motion.TypeId];
            var displayNumber = AssemblyMotionNumber.Format(type.Code, motion.Number!.Value);

            try {
                if (motion.OpenSlidesMotionId == null) {
                    if (categoryErrors.TryGetValue(type.Id, out var categoryError))
                        throw new OpenSlidesException(categoryError, CategoryFailedCode);

                    var categoryId = await EnsureCategoryAsync(settings, meetingId, type, categoryErrors);
                    await CreateAsync(settings, meetingId, motion, displayNumber, categoryId, submitters[motion.Id], nowUtc);
                    result.Created++;
                } else if (motion.OpenSlidesPushedRevision == null || motion.Revision > motion.OpenSlidesPushedRevision) {
                    await UpdateAsync(settings, motion, submitters[motion.Id], nowUtc);
                    result.Updated++;
                } else {
                    result.Skipped++;
                }
            } catch (Exception ex) {
                var error = Describe(ex, displayNumber);
                StoreError(motion.Id, error);
                result.Failures.Add(new OpenSlidesPushFailureDTO { DisplayNumber = displayNumber, Error = error });
            }
        }

        _auditLog.Log("AssemblyMotionPortal", portal.Id, "OpenSlidesPushed", null, null,
            $"created={result.Created} updated={result.Updated} skipped={result.Skipped} failed={result.Failures.Count}");
        return result;
    }

    private List<AssemblyMotion> SelectInOrder(AssemblyMotionPortal portal, bool includeSubmitted, Dictionary<Guid, AssemblyMotionType> types, DateTime nowUtc) {
        var selected = _motions.ListNumberedByPortal(portal.Id)
            .Where(m => m.Number != null)
            .Where(m => m.Status == AssemblyMotionStatus.Admitted || (includeSubmitted && m.Status == AssemblyMotionStatus.Submitted))
            .ToList();

        return AssemblyMotionExportOrder.Sort(portal, selected, types, _upvotes, nowUtc)
            .Select(entry => entry.Motion)
            .ToList();
    }

    private Dictionary<Guid, string> SubmitterNames(List<AssemblyMotion> motions) {
        return AssemblyMotionSubmitterNames.ForMotions(_db, _motions, motions)
                .ToDictionary(pair => pair.Key, pair => string.Join(", ", pair.Value));
    }

    private async Task<int> EnsureCategoryAsync(OpenSlidesSettings settings, int meetingId, AssemblyMotionType type, Dictionary<Guid, string> categoryErrors) {
        if (type.OpenSlidesCategoryId != null)
            return type.OpenSlidesCategoryId.Value;

        try {
            var categoryId = await _client.CreateCategoryAsync(settings, meetingId, type.Name, type.Code, CancellationToken.None);
            _db.AssemblyMotionTypes
                .Where(t => t.Id == type.Id)
                .Set(t => t.OpenSlidesCategoryId, categoryId)
                .Update();
            type.OpenSlidesCategoryId = categoryId;
            return categoryId;
        } catch (Exception ex) {
            var error = Describe(ex, type.Code);
            categoryErrors[type.Id] = error;
            throw new OpenSlidesException(error, CategoryFailedCode);
        }
    }

    private async Task CreateAsync(OpenSlidesSettings settings, int meetingId, AssemblyMotion motion, string displayNumber, int categoryId, string submitters, DateTime nowUtc) {
        var openSlidesId = await _client.CreateMotionAsync(settings, new OpenSlidesMotionCreate(
            meetingId, motion.Title, displayNumber, motion.TextHtml, ReasonOrNull(motion), categoryId, submitters), CancellationToken.None);

        // Committed on its own before any other bookkeeping, so a failure after the call cannot lead to a second create.
        _db.AssemblyMotions
            .Where(m => m.Id == motion.Id)
            .Set(m => m.OpenSlidesMotionId, openSlidesId)
            .Update();

        MarkPushed(motion, nowUtc);
    }

    private async Task UpdateAsync(OpenSlidesSettings settings, AssemblyMotion motion, string submitters, DateTime nowUtc) {
        await _client.UpdateMotionAsync(settings, new OpenSlidesMotionUpdate(
            motion.OpenSlidesMotionId!.Value, motion.Title, motion.TextHtml, ReasonOrNull(motion), submitters), CancellationToken.None);

        MarkPushed(motion, nowUtc);
    }

    private void MarkPushed(AssemblyMotion motion, DateTime nowUtc) {
        _db.AssemblyMotions
            .Where(m => m.Id == motion.Id)
            .Set(m => m.OpenSlidesPushedRevision, motion.Revision)
            .Set(m => m.OpenSlidesPushedAt, nowUtc)
            .Set(m => m.OpenSlidesLastError, (string?)null)
            .Update();
    }

    private void StoreError(Guid motionId, string error) {
        _db.AssemblyMotions
            .Where(m => m.Id == motionId)
            .Set(m => m.OpenSlidesLastError, error)
            .Update();
    }

    private static string? ReasonOrNull(AssemblyMotion motion) {
        return string.IsNullOrWhiteSpace(motion.ReasonHtml) ? null : motion.ReasonHtml;
    }

    private string Describe(Exception exception, string subject) {
        if (!IsKnownFailure(exception))
            _logger.LogError("Unexpected error while pushing {Subject} to OpenSlides: {Exception}", subject, exception);

        return OpenSlidesErrorText.ForAction(exception);
    }

    private static bool IsKnownFailure(Exception exception) {
        if (exception is OpenSlidesException)
            return true;
        if (exception is HttpRequestException)
            return true;
        if (exception is TimeoutException)
            return true;

        return exception is OperationCanceledException;
    }
}
