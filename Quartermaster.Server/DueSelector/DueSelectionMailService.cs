using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartermaster.Api.DueSelector;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Options;
using Quartermaster.Rendering;
using Quartermaster.Server.Messaging;
using Quartermaster.Server.Notifications;

namespace Quartermaster.Server.DueSelector;

/// <summary>
/// Directly-addressed transactional mail to a due-selection submitter when their reduced-fee
/// request is approved or rejected. Chapter context is resolved best-effort via the linked
/// membership application (a standalone selection simply has no chapter name).
/// </summary>
public class DueSelectionMailService {
    public const string ApprovedTriggerId = "dueselection_approved";
    public const string RejectedTriggerId = "dueselection_rejected";
    private const string ApprovedSubjectKey = "templates.dueselection.approved.email.subject";
    private const string ApprovedBodyKey = "templates.dueselection.approved.email.body";
    private const string RejectedSubjectKey = "templates.dueselection.rejected.email.subject";
    private const string RejectedBodyKey = "templates.dueselection.rejected.email.body";

    private readonly OptionRepository _optionRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly MembershipApplicationRepository _applicationRepo;
    private readonly EmailMessageChannel _emailChannel;
    private readonly NotificationTemplateGlobals _globals;
    private readonly ILogger<DueSelectionMailService> _logger;

    public DueSelectionMailService(
        OptionRepository optionRepo,
        ChapterRepository chapterRepo,
        MembershipApplicationRepository applicationRepo,
        EmailMessageChannel emailChannel,
        NotificationTemplateGlobals globals,
        ILogger<DueSelectionMailService> logger) {
        _optionRepo = optionRepo;
        _chapterRepo = chapterRepo;
        _applicationRepo = applicationRepo;
        _emailChannel = emailChannel;
        _globals = globals;
        _logger = logger;
    }

    public async Task SendDueSelectionDecisionAsync(DueSelection selection, CancellationToken ct) {
        if (selection.Status != DueSelectionStatus.Approved && selection.Status != DueSelectionStatus.Rejected) {
            return;
        }
        if (string.IsNullOrWhiteSpace(selection.Email)) {
            _logger.LogWarning("Due-selection decision mail skipped: empty email for selection {Id}", selection.Id);
            return;
        }

        var approved = selection.Status == DueSelectionStatus.Approved;
        var subjectKey = approved ? ApprovedSubjectKey : RejectedSubjectKey;
        var bodyKey = approved ? ApprovedBodyKey : RejectedBodyKey;
        var triggerId = approved ? ApprovedTriggerId : RejectedTriggerId;

        var subjectTpl = _optionRepo.ResolveValue(subjectKey, null, _chapterRepo);
        var bodyTpl = _optionRepo.ResolveValue(bodyKey, null, _chapterRepo);
        if (string.IsNullOrEmpty(subjectTpl) || string.IsNullOrEmpty(bodyTpl)) {
            _logger.LogWarning("Due-selection decision templates missing ({SubjectKey}/{BodyKey})", subjectKey, bodyKey);
            return;
        }

        var model = new Dictionary<string, object> {
            ["globals"] = _globals.Build(),
            ["selection"] = new {
                selection.Id,
                selection.FirstName,
                selection.LastName,
                selection.Email,
                selection.SelectedDue,
                selection.ReducedAmount,
                selection.ReducedJustification
            },
            ["chapter"] = new { Name = ChapterName(selection) }
        };

        var (subject, _) = await TemplateRenderer.RenderTextAsync(subjectTpl, model);
        var (body, _) = await TemplateRenderer.RenderHtmlAsync(bodyTpl, model);

        var metadata = new Dictionary<string, string> {
            [NotificationLogMetadataKeys.TriggerId] = triggerId,
            [NotificationLogMetadataKeys.TemplateIdentifier] = bodyKey
        };
        await _emailChannel.SendAsync(new ChannelMessage(
            ChannelAddress: selection.Email!,
            Subject: subject ?? subjectTpl,
            Body: body ?? bodyTpl,
            SourceEntityType: "DueSelection",
            SourceEntityId: selection.Id,
            Metadata: metadata), ct);
    }

    private string ChapterName(DueSelection selection) {
        var application = _applicationRepo.GetByDueSelectionId(selection.Id);
        if (application == null || !application.ChapterId.HasValue) {
            return "";
        }
        return _chapterRepo.Get(application.ChapterId.Value)?.Name ?? "";
    }
}
