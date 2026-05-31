using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.DueSelector;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Templates;
using Quartermaster.Rendering;
using Quartermaster.Server.Messaging;
using Quartermaster.Server.Notifications;

namespace Quartermaster.Server.DueSelector;

public class DueSelectionMailService {
    public const string ApprovedTriggerId = "dueselection_approved";
    public const string RejectedTriggerId = "dueselection_rejected";
    private const string ApprovedTemplateId = "templates.dueselection.approved.email";
    private const string RejectedTemplateId = "templates.dueselection.rejected.email";

    private readonly TemplateRepository _templateRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly MembershipApplicationRepository _applicationRepo;
    private readonly EmailMessageChannel _emailChannel;
    private readonly NotificationTemplateGlobals _globals;
    private readonly ILogger<DueSelectionMailService> _logger;

    public DueSelectionMailService(
        TemplateRepository templateRepo,
        ChapterRepository chapterRepo,
        MembershipApplicationRepository applicationRepo,
        EmailMessageChannel emailChannel,
        NotificationTemplateGlobals globals,
        ILogger<DueSelectionMailService> logger) {
        _templateRepo = templateRepo;
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
        var templateIdentifier = approved ? ApprovedTemplateId : RejectedTemplateId;
        var triggerId = approved ? ApprovedTriggerId : RejectedTriggerId;

        var application = _applicationRepo.GetByDueSelectionId(selection.Id);
        var chapterId = application?.ChapterId;
        var chapter = chapterId.HasValue ? _chapterRepo.Get(chapterId.Value) : null;

        var template = _templateRepo.Resolve(templateIdentifier, chapterId, _chapterRepo);
        if (template == null || string.IsNullOrEmpty(template.Body)) {
            _logger.LogWarning("Due-selection decision template missing ({Identifier})", templateIdentifier);
            return;
        }

        var model = new Dictionary<string, object> {
            ["globals"] = _globals.Build(),
            ["selection"] = selection.ToDetailDto(),
            ["chapter"] = chapter?.ToDto() ?? new ChapterDTO()
        };

        var (subject, _) = await TemplateRenderer.RenderTextAsync(template.Subject ?? "", model);
        var (body, _) = await TemplateRenderer.RenderHtmlAsync(template.Body, model);

        var metadata = new Dictionary<string, string> {
            [NotificationLogMetadataKeys.TriggerId] = triggerId,
            [NotificationLogMetadataKeys.TemplateIdentifier] = templateIdentifier
        };
        await _emailChannel.SendAsync(new ChannelMessage(
            ChannelAddress: selection.Email!,
            Subject: subject ?? template.Subject ?? "",
            Body: body ?? template.Body,
            SourceEntityType: "DueSelection",
            SourceEntityId: selection.Id,
            Metadata: metadata), ct);
    }
}
