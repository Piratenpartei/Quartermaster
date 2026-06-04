using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.ChapterMails;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.ChapterMails;
using Quartermaster.Data.Chapters;
using Quartermaster.Rendering;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.Email;

namespace Quartermaster.Server.ChapterMails;

public class ChapterMailSendResponse {
    public int RecipientCount { get; set; }
}

public class ChapterMailSendEndpoint : Endpoint<ChapterMailIdRequest, ChapterMailSendResponse> {
    private readonly ChapterMailRepository _mailRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly AdministrativeDivisionRepository _adminDivRepo;
    private readonly EmailService _emailService;
    private readonly PermissionContext _perms;

    public ChapterMailSendEndpoint(ChapterMailRepository mailRepo, ChapterRepository chapterRepo,
        AdministrativeDivisionRepository adminDivRepo, EmailService emailService, PermissionContext perms) {
        _mailRepo = mailRepo;
        _chapterRepo = chapterRepo;
        _adminDivRepo = adminDivRepo;
        _emailService = emailService;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/chapter-mail/{Id}/send");
    }

    public override async Task HandleAsync(ChapterMailIdRequest req, CancellationToken ct) {
        var mail = _mailRepo.Get(req.Id);
        if (mail == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.Has(mail.ChapterId, PermissionIdentifier.ManageChapterMail)) {
            await SendForbiddenAsync(ct);
            return;
        }
        if (mail.IsSent) {
            AddError("mail has already been sent");
            await SendErrorsAsync(409, ct);
            return;
        }
        if (mail.IsArchived) {
            AddError("mail is archived and cannot be sent");
            await SendErrorsAsync(409, ct);
            return;
        }
        var isManual = mail.TemplateId == null;
        if (isManual && (string.IsNullOrWhiteSpace(mail.Subject) || string.IsNullOrWhiteSpace(mail.Content))) {
            AddError("manual mail needs a subject and content (or pick a template)");
            await SendErrorsAsync(400, ct);
            return;
        }

        Dictionary<string, object> ModelFor(Data.Members.Member? member) {
            var model = new Dictionary<string, object>();
            if (member != null) {
                model["member"] = member.ToDetailDto();
            }
            return model;
        }

        var targetTypeWire = mail.TargetType == ChapterMailTargetType.Chapter ? "Chapter" : "AdministrativeDivision";
        var descriptionOverride = isManual
            ? MarkdownService.ToHtml(mail.Content ?? "", SanitizationProfile.Standard)
            : null;
        var subjectOverride = isManual ? mail.Subject : null;

        var (count, error) = await _emailService.SendEmailAsync(
            targetTypeWire, mail.TargetId, mail.TemplateId,
            descriptionOverride: descriptionOverride, manualAddresses: null,
            sourceEntityType: "ChapterMail", sourceEntityId: mail.Id,
            resolutionChapterId: mail.ChapterId,
            modelFactory: ModelFor,
            descendChapterTree: mail.IncludeDescendants,
            subjectOverride: subjectOverride);

        if (error != null) {
            AddError(error);
            await SendErrorsAsync(500, ct);
            return;
        }

        _mailRepo.MarkSent(mail.Id, _perms.UserId.Value, count, DateTime.UtcNow);
        await SendAsync(new ChapterMailSendResponse { RecipientCount = count }, cancellation: ct);
    }

}
