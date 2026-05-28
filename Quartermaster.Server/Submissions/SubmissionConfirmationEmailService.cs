using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Api.Motions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Options;
using Quartermaster.Data.Submissions;
using Quartermaster.Rendering;
using Quartermaster.Server.Messaging;
using Quartermaster.Server.Notifications;

namespace Quartermaster.Server.Submissions;

/// <summary>
/// Sends the "please confirm your email" message to a public submitter. The body template
/// is admin-configurable per submission kind and gets a <c>confirm</c> model (the confirm
/// link) plus a kind-specific summary. Sent transactionally via the email channel.
/// </summary>
public class SubmissionConfirmationEmailService {
    private readonly OptionRepository _optionRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly EmailMessageChannel _emailChannel;
    private readonly NotificationTemplateGlobals _globals;
    private readonly ILogger<SubmissionConfirmationEmailService> _logger;

    public SubmissionConfirmationEmailService(
        OptionRepository optionRepo,
        ChapterRepository chapterRepo,
        EmailMessageChannel emailChannel,
        NotificationTemplateGlobals globals,
        ILogger<SubmissionConfirmationEmailService> logger) {
        _optionRepo = optionRepo;
        _chapterRepo = chapterRepo;
        _emailChannel = emailChannel;
        _globals = globals;
        _logger = logger;
    }

    public async Task SendAsync(PendingSubmissionKind kind, object request, string token, string email, CancellationToken ct) {
        if (string.IsNullOrWhiteSpace(email)) {
            _logger.LogWarning("Submission confirmation skipped: empty email for {Kind}", kind);
            return;
        }

        var globals = _globals.Build();
        var baseUrl = globals.TryGetValue("base_url", out var b) ? b as string ?? "" : "";
        var model = new Dictionary<string, object> {
            ["globals"] = globals,
            ["confirm"] = new { url = $"{baseUrl}/Confirm/{token}" }
        };
        var (subjectKey, bodyKey) = TemplateKeys(kind);
        AddSummaryModel(kind, request, model);

        var subjectTpl = _optionRepo.ResolveValue(subjectKey, null, _chapterRepo);
        var bodyTpl = _optionRepo.ResolveValue(bodyKey, null, _chapterRepo);
        if (string.IsNullOrEmpty(subjectTpl) || string.IsNullOrEmpty(bodyTpl)) {
            _logger.LogWarning("Submission confirmation templates missing for {Kind} ({SubjectKey}/{BodyKey})", kind, subjectKey, bodyKey);
            return;
        }

        var (subject, _) = await TemplateRenderer.RenderTextAsync(subjectTpl, model);
        var (body, _) = await TemplateRenderer.RenderHtmlAsync(bodyTpl, model);

        var metadata = new Dictionary<string, string> {
            [NotificationLogMetadataKeys.TriggerId] = $"submission_confirmation_{kind}".ToLowerInvariant(),
            [NotificationLogMetadataKeys.TemplateIdentifier] = bodyKey
        };
        await _emailChannel.SendAsync(new ChannelMessage(
            ChannelAddress: email,
            Subject: subject ?? subjectTpl,
            Body: body ?? bodyTpl,
            Metadata: metadata), ct);
    }

    private static (string SubjectKey, string BodyKey) TemplateKeys(PendingSubmissionKind kind) => kind switch {
        PendingSubmissionKind.Motion => (
            "templates.submission.motion.confirmation.email.subject",
            "templates.submission.motion.confirmation.email.body"),
        PendingSubmissionKind.DueSelection => (
            "templates.submission.dueselection.confirmation.email.subject",
            "templates.submission.dueselection.confirmation.email.body"),
        PendingSubmissionKind.MembershipApplication => (
            "templates.submission.membershipapplication.confirmation.email.subject",
            "templates.submission.membershipapplication.confirmation.email.body"),
        _ => ("", "")
    };

    private void AddSummaryModel(PendingSubmissionKind kind, object request, Dictionary<string, object> model) {
        switch (kind) {
            case PendingSubmissionKind.Motion when request is MotionCreateRequest m:
                model["motion"] = new { m.Title, m.AuthorName };
                model["chapter"] = new { Name = _chapterRepo.Get(m.ChapterId)?.Name ?? "" };
                break;
            case PendingSubmissionKind.DueSelection when request is DueSelectionDTO d:
                model["selection"] = new { d.FirstName, d.LastName, d.SelectedDue, d.ReducedAmount };
                break;
            case PendingSubmissionKind.MembershipApplication when request is MembershipApplicationDTO a:
                model["application"] = new { a.FirstName, a.LastName, a.Email };
                model["chapter"] = new { Name = a.ChapterId.HasValue ? _chapterRepo.Get(a.ChapterId.Value)?.Name ?? "" : "" };
                break;
        }
    }
}
