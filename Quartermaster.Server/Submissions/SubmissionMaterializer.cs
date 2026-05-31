using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartermaster.Api;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Api.Motions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.Members;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Motions;
using Quartermaster.Data.Submissions;
using Quartermaster.Rendering;
using Quartermaster.Server.MembershipApplications;
using Quartermaster.Server.Notifications;

namespace Quartermaster.Server.Submissions;

/// <summary>
/// Creates the real entity (and fires officer notifications) for a confirmed public
/// submission. The create endpoints only stash the request; this runs at confirm time,
/// so notifications never fire for unconfirmed spam.
/// </summary>
public class SubmissionMaterializer {
    private readonly MotionRepository _motionRepo;
    private readonly DueSelectionRepository _dueSelectionRepo;
    private readonly MembershipApplicationRepository _applicationRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly MemberRepository _memberRepo;
    private readonly INotificationDispatchQueue _notifications;
    private readonly MembershipApplicationMailService _applicantMail;
    private readonly ApplicationReviewService _reviewService;
    private readonly ILogger<SubmissionMaterializer> _logger;

    public SubmissionMaterializer(
        MotionRepository motionRepo,
        DueSelectionRepository dueSelectionRepo,
        MembershipApplicationRepository applicationRepo,
        ChapterRepository chapterRepo,
        MemberRepository memberRepo,
        INotificationDispatchQueue notifications,
        MembershipApplicationMailService applicantMail,
        ApplicationReviewService reviewService,
        ILogger<SubmissionMaterializer> logger) {
        _motionRepo = motionRepo;
        _dueSelectionRepo = dueSelectionRepo;
        _applicationRepo = applicationRepo;
        _chapterRepo = chapterRepo;
        _memberRepo = memberRepo;
        _notifications = notifications;
        _applicantMail = applicantMail;
        _reviewService = reviewService;
        _logger = logger;
    }

    public async Task MaterializeAsync(PendingSubmission pending, CancellationToken ct = default) {
        switch (pending.Kind) {
            case PendingSubmissionKind.Motion:
                MaterializeMotion(Deserialize<MotionCreateRequest>(pending.PayloadJson));
                break;
            case PendingSubmissionKind.DueSelection:
                MaterializeDueSelection(Deserialize<DueSelectionDTO>(pending.PayloadJson));
                break;
            case PendingSubmissionKind.MembershipApplication:
                await MaterializeApplicationAsync(Deserialize<MembershipApplicationDTO>(pending.PayloadJson), ct);
                break;
            default:
                _logger.LogWarning("Unknown pending submission kind {Kind}", pending.Kind);
                break;
        }
    }

    /// <summary>
    /// Direct-path overload for authenticated callers: materialize a request without going through
    /// the email-confirm spam barrier. Returns the new entity's id, or <c>null</c> if the request
    /// referenced a missing chapter and was dropped.
    /// </summary>
    public Guid? MaterializeMotionDirect(MotionCreateRequest req) => MaterializeMotion(req);

    /// <summary>Direct-path overload for authenticated callers — see <see cref="MaterializeMotionDirect"/>.</summary>
    public Guid MaterializeDueSelectionDirect(DueSelectionDTO req) => MaterializeDueSelection(req);

    /// <summary>Direct-path overload for authenticated callers — see <see cref="MaterializeMotionDirect"/>.</summary>
    public Task<Guid> MaterializeApplicationDirectAsync(MembershipApplicationDTO req, CancellationToken ct = default)
        => MaterializeApplicationAsync(req, ct);

    private Guid? MaterializeMotion(MotionCreateRequest req) {
        var chapter = _chapterRepo.Get(req.ChapterId);
        if (chapter == null) {
            _logger.LogWarning("Confirmed motion references a chapter that no longer exists: {ChapterId}", req.ChapterId);
            return null;
        }

        var textHtml = MarkdownService.ToHtml(req.Text, SanitizationProfile.Strict);
        var motion = Motion.FromCreateRequest(req, textHtml, DateTime.UtcNow);
        _motionRepo.Create(motion);

        var chapterName = chapter.Name;
        var payload = new MotionSubmittedPayload(
            motion.Id, motion.ChapterId, motion.Title, motion.AuthorName, chapterName);
        _notifications.Enqueue(new NotificationDispatchRequest(
            NotificationTriggers.MotionSubmitted,
            payload,
            _ => new Dictionary<string, object> {
                ["motion"] = motion.ToDetailDto(chapterName),
                ["chapter"] = chapter.ToDto()
            },
            SourceEntityType: "Motion",
            SourceEntityId: motion.Id));
        return motion.Id;
    }

    private Guid MaterializeDueSelection(DueSelectionDTO req) {
        var dueSelection = DueSelection.FromDto(req);
        _dueSelectionRepo.Create(dueSelection);

        if (req.MemberNumber > 0) {
            var member = _memberRepo.GetByMemberNumber(req.MemberNumber);
            if (member?.ChapterId != null) {
                var chapterId = member.ChapterId.Value;
                var chapter = _chapterRepo.Get(chapterId);
                var chapterName = chapter?.Name ?? "";
                var payload = new DueSelectionSubmittedPayload(
                    dueSelection.Id, chapterId, chapterName,
                    dueSelection.FirstName, dueSelection.LastName, dueSelection.SelectedDue);
                _notifications.Enqueue(new NotificationDispatchRequest(
                    NotificationTriggers.DueSelectionSubmitted,
                    payload,
                    _ => new Dictionary<string, object> {
                        ["selection"] = dueSelection.ToDetailDto(),
                        ["chapter"] = chapter?.ToDto() ?? new ChapterDTO { Id = chapterId, Name = chapterName }
                    },
                    SourceEntityType: "DueSelection",
                    SourceEntityId: dueSelection.Id));
            }
        }
        return dueSelection.Id;
    }

    private async Task<Guid> MaterializeApplicationAsync(MembershipApplicationDTO req, CancellationToken ct) {
        Guid? dueSelectionId = null;
        var isReduced = false;
        if (req.DueSelection != null) {
            var dueSelection = DueSelection.FromDto(req.DueSelection);
            isReduced = dueSelection.SelectedValuation == SelectedValuation.Reduced;
            dueSelection.Status = isReduced
                ? DueSelectionStatus.Pending
                : DueSelectionStatus.AutoApproved;
            _dueSelectionRepo.Create(dueSelection);
            dueSelectionId = dueSelection.Id;
        }

        var application = MembershipApplication.FromDto(req, dueSelectionId, DateTime.UtcNow);
        _applicationRepo.Create(application);

        await _applicantMail.SendApplicationReceivedAsync(application, isReduced, ct);

        // Without a chapter the application waits in PendingDivisionLinking (set above) — no review
        // motion, no officer notification — until someone assigns a chapter via the linking endpoint.
        if (!application.ChapterId.HasValue) {
            return application.Id;
        }

        _reviewService.CreateReviewMotionAndNotify(application);
        return application.Id;
    }

    private static T Deserialize<T>(string json) {
        return JsonSerializer.Deserialize<T>(json)
            ?? throw new InvalidOperationException($"Pending submission payload deserialized to null for {typeof(T).Name}");
    }
}
