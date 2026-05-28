using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Options;
using Quartermaster.Rendering;
using Quartermaster.Server.Messaging;
using Quartermaster.Server.Notifications;

namespace Quartermaster.Server.MembershipApplications;

/// <summary>
/// Directly-addressed transactional mails to a membership applicant (not chapter-perm
/// broadcasts): the "Antrag eingegangen" acknowledgement sent once the applicant confirms
/// their email, and the welcome mail an officer triggers manually after assigning a member
/// number. Both render an admin-configurable template and hand off to the email channel.
/// </summary>
public class MembershipApplicationMailService {
    public const string ReceivedTriggerId = "application_received";
    public const string WelcomeTriggerId = "member_welcome";
    public const string ApprovedTriggerId = "application_approved";
    public const string RejectedTriggerId = "application_rejected";
    private const string ReceivedSubjectKey = "templates.membershipapplication.received.email.subject";
    private const string ReceivedBodyKey = "templates.membershipapplication.received.email.body";
    private const string WelcomeSubjectKey = "templates.member.welcome.email.subject";
    private const string WelcomeBodyKey = "templates.member.welcome.email.body";
    private const string ApprovedSubjectKey = "templates.membershipapplication.approved.email.subject";
    private const string ApprovedBodyKey = "templates.membershipapplication.approved.email.body";
    private const string RejectedSubjectKey = "templates.membershipapplication.rejected.email.subject";
    private const string RejectedBodyKey = "templates.membershipapplication.rejected.email.body";

    private readonly OptionRepository _optionRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly EmailMessageChannel _emailChannel;
    private readonly NotificationTemplateGlobals _globals;
    private readonly ILogger<MembershipApplicationMailService> _logger;

    public MembershipApplicationMailService(
        OptionRepository optionRepo,
        ChapterRepository chapterRepo,
        EmailMessageChannel emailChannel,
        NotificationTemplateGlobals globals,
        ILogger<MembershipApplicationMailService> logger) {
        _optionRepo = optionRepo;
        _chapterRepo = chapterRepo;
        _emailChannel = emailChannel;
        _globals = globals;
        _logger = logger;
    }

    public async Task SendApplicationReceivedAsync(MembershipApplication application, bool isReduced, CancellationToken ct) {
        var model = new Dictionary<string, object> {
            ["globals"] = _globals.Build(),
            ["application"] = new {
                application.Id,
                application.FirstName,
                application.LastName,
                application.Email,
                application.SubmittedAt,
                HasReducedDueSelection = isReduced
            },
            ["chapter"] = new { Name = ChapterName(application) }
        };
        await SendAsync(ReceivedTriggerId, ReceivedSubjectKey, ReceivedBodyKey, application, application.Email, model, ct);
    }

    public async Task SendWelcomeAsync(MembershipApplication application, int memberNumber, CancellationToken ct) {
        var model = new Dictionary<string, object> {
            ["globals"] = _globals.Build(),
            ["member"] = new {
                application.FirstName,
                application.LastName,
                application.Email,
                MemberNumber = memberNumber
            },
            ["chapter"] = new { Name = ChapterName(application) }
        };
        await SendAsync(WelcomeTriggerId, WelcomeSubjectKey, WelcomeBodyKey, application, application.Email, model, ct);
    }

    public async Task SendApplicationDecisionAsync(MembershipApplication application, CancellationToken ct) {
        if (application.Status != ApplicationStatus.Approved && application.Status != ApplicationStatus.Rejected) {
            return;
        }
        var approved = application.Status == ApplicationStatus.Approved;
        var model = new Dictionary<string, object> {
            ["globals"] = _globals.Build(),
            ["application"] = new {
                application.Id,
                application.FirstName,
                application.LastName,
                application.Email,
                application.MemberNumber,
                application.Status
            },
            ["chapter"] = new { Name = ChapterName(application) }
        };
        await SendAsync(
            approved ? ApprovedTriggerId : RejectedTriggerId,
            approved ? ApprovedSubjectKey : RejectedSubjectKey,
            approved ? ApprovedBodyKey : RejectedBodyKey,
            application, application.Email, model, ct);
    }

    private string ChapterName(MembershipApplication application) {
        if (!application.ChapterId.HasValue)
            return "";
        return _chapterRepo.Get(application.ChapterId.Value)?.Name ?? "";
    }

    private async Task SendAsync(string triggerId, string subjectKey, string bodyKey,
        MembershipApplication application, string email, Dictionary<string, object> model, CancellationToken ct) {
        if (string.IsNullOrWhiteSpace(email)) {
            _logger.LogWarning("Applicant mail '{TriggerId}' skipped: empty email for application {Id}", triggerId, application.Id);
            return;
        }

        var subjectTpl = _optionRepo.ResolveValue(subjectKey, null, _chapterRepo);
        var bodyTpl = _optionRepo.ResolveValue(bodyKey, null, _chapterRepo);
        if (string.IsNullOrEmpty(subjectTpl) || string.IsNullOrEmpty(bodyTpl)) {
            _logger.LogWarning("Applicant mail '{TriggerId}' templates missing ({SubjectKey}/{BodyKey})", triggerId, subjectKey, bodyKey);
            return;
        }

        var (subject, _) = await TemplateRenderer.RenderTextAsync(subjectTpl, model);
        var (body, _) = await TemplateRenderer.RenderHtmlAsync(bodyTpl, model);

        var metadata = new Dictionary<string, string> {
            [NotificationLogMetadataKeys.TriggerId] = triggerId,
            [NotificationLogMetadataKeys.TemplateIdentifier] = bodyKey
        };
        await _emailChannel.SendAsync(new ChannelMessage(
            ChannelAddress: email,
            Subject: subject ?? subjectTpl,
            Body: body ?? bodyTpl,
            SourceEntityType: "MembershipApplication",
            SourceEntityId: application.Id,
            Metadata: metadata), ct);
    }
}
