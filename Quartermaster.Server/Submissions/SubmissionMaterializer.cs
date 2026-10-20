using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Api.Motions;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Forwarding;
using Quartermaster.Data.Members;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Motions;
using Quartermaster.Data.Submissions;
using Quartermaster.Rendering;
using Quartermaster.Server.Forwarding;
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
    private readonly MembershipApplicationRepository _applicationRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly MemberRepository _memberRepo;
    private readonly INotificationDispatchQueue _notifications;
    private readonly MembershipApplicationMailService _applicantMail;
    private readonly ForwardingIntake _forwardingIntake;
    private readonly AdministrativeDivisionRepository _divisionRepo;
    private readonly ILogger<SubmissionMaterializer> _logger;

    public SubmissionMaterializer(
        MotionRepository motionRepo,
        MembershipApplicationRepository applicationRepo,
        ChapterRepository chapterRepo,
        MemberRepository memberRepo,
        INotificationDispatchQueue notifications,
        MembershipApplicationMailService applicantMail,
        ForwardingIntake forwardingIntake,
        AdministrativeDivisionRepository divisionRepo,
        ILogger<SubmissionMaterializer> logger) {
        _motionRepo = motionRepo;
        _applicationRepo = applicationRepo;
        _chapterRepo = chapterRepo;
        _memberRepo = memberRepo;
        _notifications = notifications;
        _applicantMail = applicantMail;
        _forwardingIntake = forwardingIntake;
        _divisionRepo = divisionRepo;
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
    public Task<Guid> MaterializeApplicationDirectAsync(MembershipApplicationDTO req, CancellationToken ct = default)
        => MaterializeApplicationAsync(req, ct);

    private Guid? MaterializeMotion(MotionCreateRequest req) {
        var chapter = _chapterRepo.Get(req.ChapterId);
        if (chapter == null) {
            _logger.LogWarning("Confirmed motion references a chapter that no longer exists: {ChapterId}", req.ChapterId);
            return null;
        }
        if (chapter.ArchivedAt.HasValue) {
            // Chapter was archived between submission and email confirmation; drop the motion silently.
            _logger.LogInformation("Confirmed motion targets an archived chapter; skipping: {ChapterId}", req.ChapterId);
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

    /// <summary>
    /// Confirms a due selection that was submitted anonymously before V7 made the form login-only. Routed like a logged-in submission without a linked account:
    /// the chapter comes from the member number, the person data counts as entered by hand.
    /// </summary>
    private void MaterializeDueSelection(DueSelectionDTO req) {
        var member = req.MemberNumber != 0 ? _memberRepo.GetByMemberNumber(req.MemberNumber) : null;
        _forwardingIntake.IntakeDueSelection(req, member?.ChapterId, null, null, true, DateTime.UtcNow);
    }

    /// <summary>
    /// The chapter comes from the address' administrative division, never from the client. The receipt mail always goes out, also when the application is only
    /// forwarded to Redmine; returns the stored application id, or the forwarding case id when Quartermaster keeps no application.
    /// </summary>
    private async Task<Guid> MaterializeApplicationAsync(MembershipApplicationDTO req, CancellationToken ct) {
        var chapterId = req.AddressAdministrativeDivisionId == null
            ? null
            : _chapterRepo.FindForDivision(req.AddressAdministrativeDivisionId.Value, _divisionRepo)?.Id;
        req.ChapterId = chapterId;

        var result = _forwardingIntake.IntakeApplication(req, chapterId, DateTime.UtcNow);
        var application = result.ApplicationId == null ? null : _applicationRepo.Get(result.ApplicationId.Value);
        // Without a stored application the mail is filed under the case, so its text is deleted together with the case's data.
        (string Type, Guid Id)? logSource = application == null && result.CaseId != null ? (ForwardingCaseRepository.MailSourceType, result.CaseId.Value) : null;
        application ??= MembershipApplication.FromDto(req, null, DateTime.UtcNow);
        await _applicantMail.SendApplicationReceivedAsync(application, result.IsReduced, logSource, ct);

        return result.ApplicationId ?? result.CaseId ?? application.Id;
    }

    private static T Deserialize<T>(string json) {
        return JsonSerializer.Deserialize<T>(json)
            ?? throw new InvalidOperationException($"Pending submission payload deserialized to null for {typeof(T).Name}");
    }
}
