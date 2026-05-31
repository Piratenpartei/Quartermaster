using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Templates;
using Quartermaster.Rendering;
using Quartermaster.Server.Messaging;
using Quartermaster.Server.Notifications;

namespace Quartermaster.Server.MembershipApplications;

public class MembershipApplicationMailService {
    public const string ReceivedTriggerId = "application_received";
    public const string WelcomeTriggerId = "member_welcome";
    public const string ApprovedTriggerId = "application_approved";
    public const string RejectedTriggerId = "application_rejected";
    private const string ReceivedTemplateId = "templates.membershipapplication.received.email";
    private const string WelcomeTemplateId = "templates.member.welcome.email";
    private const string ApprovedTemplateId = "templates.membershipapplication.approved.email";
    private const string RejectedTemplateId = "templates.membershipapplication.rejected.email";

    private readonly TemplateRepository _templateRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly EmailMessageChannel _emailChannel;
    private readonly NotificationTemplateGlobals _globals;
    private readonly ILogger<MembershipApplicationMailService> _logger;

    public MembershipApplicationMailService(
        TemplateRepository templateRepo,
        ChapterRepository chapterRepo,
        EmailMessageChannel emailChannel,
        NotificationTemplateGlobals globals,
        ILogger<MembershipApplicationMailService> logger) {
        _templateRepo = templateRepo;
        _chapterRepo = chapterRepo;
        _emailChannel = emailChannel;
        _globals = globals;
        _logger = logger;
    }

    public async Task SendApplicationReceivedAsync(MembershipApplication application, bool isReduced, CancellationToken ct) {
        var chapter = ResolveChapter(application);
        var model = new Dictionary<string, object> {
            ["globals"] = _globals.Build(),
            ["application"] = application.ToDetailDto(chapter?.Name ?? "", isReduced),
            ["chapter"] = chapter?.ToDto() ?? new ChapterDTO()
        };
        await SendAsync(ReceivedTriggerId, ReceivedTemplateId, application, application.Email, model, ct);
    }

    public async Task SendWelcomeAsync(MembershipApplication application, int memberNumber, CancellationToken ct) {
        var chapter = ResolveChapter(application);
        var model = new Dictionary<string, object> {
            ["globals"] = _globals.Build(),
            ["member"] = application.ToPendingMemberDto(memberNumber, chapter?.Name ?? ""),
            ["chapter"] = chapter?.ToDto() ?? new ChapterDTO()
        };
        await SendAsync(WelcomeTriggerId, WelcomeTemplateId, application, application.Email, model, ct);
    }

    public async Task SendApplicationDecisionAsync(MembershipApplication application, CancellationToken ct) {
        if (application.Status != ApplicationStatus.Approved && application.Status != ApplicationStatus.Rejected) {
            return;
        }
        var approved = application.Status == ApplicationStatus.Approved;
        var chapter = ResolveChapter(application);
        var model = new Dictionary<string, object> {
            ["globals"] = _globals.Build(),
            ["application"] = application.ToDetailDto(chapter?.Name ?? "", hasReducedDueSelection: false),
            ["chapter"] = chapter?.ToDto() ?? new ChapterDTO()
        };
        await SendAsync(
            approved ? ApprovedTriggerId : RejectedTriggerId,
            approved ? ApprovedTemplateId : RejectedTemplateId,
            application, application.Email, model, ct);
    }

    private Chapter? ResolveChapter(MembershipApplication application)
        => application.ChapterId.HasValue ? _chapterRepo.Get(application.ChapterId.Value) : null;

    private async Task SendAsync(string triggerId, string templateIdentifier,
        MembershipApplication application, string email, Dictionary<string, object> model, CancellationToken ct) {
        if (string.IsNullOrWhiteSpace(email)) {
            _logger.LogWarning("Applicant mail '{TriggerId}' skipped: empty email for application {Id}", triggerId, application.Id);
            return;
        }

        var template = _templateRepo.Resolve(templateIdentifier, application.ChapterId, _chapterRepo);
        if (template == null || string.IsNullOrEmpty(template.Body)) {
            _logger.LogWarning("Applicant mail '{TriggerId}' template missing ({Identifier})", triggerId, templateIdentifier);
            return;
        }

        var (subject, _) = await TemplateRenderer.RenderTextAsync(template.Subject ?? "", model);
        var (body, _) = await TemplateRenderer.RenderHtmlAsync(template.Body, model);

        var metadata = new Dictionary<string, string> {
            [NotificationLogMetadataKeys.TriggerId] = triggerId,
            [NotificationLogMetadataKeys.TemplateIdentifier] = templateIdentifier
        };
        await _emailChannel.SendAsync(new ChannelMessage(
            ChannelAddress: email,
            Subject: subject ?? template.Subject ?? "",
            Body: body ?? template.Body,
            SourceEntityType: "MembershipApplication",
            SourceEntityId: application.Id,
            Metadata: metadata), ct);
    }
}
