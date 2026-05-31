using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Api.Motions;
using Quartermaster.Api.Templates;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Submissions;
using Quartermaster.Data.Templates;
using Quartermaster.Rendering;
using Quartermaster.Server.Messaging;
using Quartermaster.Server.Notifications;


namespace Quartermaster.Server.Submissions;

public class SubmissionConfirmationEmailService {
    private readonly TemplateRepository _templateRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly EmailMessageChannel _emailChannel;
    private readonly NotificationTemplateGlobals _globals;
    private readonly ILogger<SubmissionConfirmationEmailService> _logger;

    public SubmissionConfirmationEmailService(
        TemplateRepository templateRepo,
        ChapterRepository chapterRepo,
        EmailMessageChannel emailChannel,
        NotificationTemplateGlobals globals,
        ILogger<SubmissionConfirmationEmailService> logger) {
        _templateRepo = templateRepo;
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
        var baseUrl = globals.TryGetValue("BaseUrl", out var b) ? b as string ?? "" : "";
        var model = new Dictionary<string, object> {
            ["globals"] = globals,
            ["confirm"] = new TemplateConfirmationDTO { Url = $"{baseUrl}/Confirm/{token}" }
        };
        var templateIdentifier = TemplateIdentifier(kind);
        var chapterId = AddSummaryModel(kind, request, model);

        var template = _templateRepo.Resolve(templateIdentifier, chapterId, _chapterRepo);
        if (template == null || string.IsNullOrEmpty(template.Body)) {
            _logger.LogWarning("Submission confirmation template missing for {Kind} ({Identifier})", kind, templateIdentifier);
            return;
        }

        var (subject, _) = await TemplateRenderer.RenderTextAsync(template.Subject ?? "", model);
        var (body, _) = await TemplateRenderer.RenderHtmlAsync(template.Body, model);

        var metadata = new Dictionary<string, string> {
            [NotificationLogMetadataKeys.TriggerId] = $"submission_confirmation_{kind}".ToLowerInvariant(),
            [NotificationLogMetadataKeys.TemplateIdentifier] = templateIdentifier
        };
        await _emailChannel.SendAsync(new ChannelMessage(
            ChannelAddress: email,
            Subject: subject ?? template.Subject ?? "",
            Body: body ?? template.Body,
            Metadata: metadata), ct);
    }

    private static string TemplateIdentifier(PendingSubmissionKind kind) => kind switch {
        PendingSubmissionKind.Motion => "templates.submission.motion.confirmation.email",
        PendingSubmissionKind.DueSelection => "templates.submission.dueselection.confirmation.email",
        PendingSubmissionKind.MembershipApplication => "templates.submission.membershipapplication.confirmation.email",
        _ => ""
    };

    private Guid? AddSummaryModel(PendingSubmissionKind kind, object request, Dictionary<string, object> model) {
        switch (kind) {
            case PendingSubmissionKind.Motion when request is MotionCreateRequest m: {
                var chapter = _chapterRepo.Get(m.ChapterId);
                model["motion"] = m.ToDetailDto(chapter?.Name ?? "");
                model["chapter"] = chapter?.ToDto() ?? new ChapterDTO { Id = m.ChapterId };
                return m.ChapterId;
            }
            case PendingSubmissionKind.DueSelection when request is DueSelectionDTO d: {
                model["selection"] = d.ToDetailDto();
                return null;
            }
            case PendingSubmissionKind.MembershipApplication when request is MembershipApplicationDTO a: {
                var chapter = a.ChapterId.HasValue ? _chapterRepo.Get(a.ChapterId.Value) : null;
                model["application"] = a.ToDetailDto(chapter?.Name ?? "");
                model["chapter"] = chapter?.ToDto() ?? new ChapterDTO { Id = a.ChapterId ?? Guid.Empty };
                return a.ChapterId;
            }
        }
        return null;
    }
}
