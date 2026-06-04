using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Events;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.ChapterMails;
using Quartermaster.Data.Chapters;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.Email;

namespace Quartermaster.Server.ChapterMails;

public class ChapterMailRecipientPreviewEndpoint : Endpoint<ChapterMailIdRequest, RecipientPreviewResponse> {
    private readonly ChapterMailRepository _mailRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly AdministrativeDivisionRepository _adminDivRepo;
    private readonly EmailService _emailService;
    private readonly PermissionContext _perms;

    public ChapterMailRecipientPreviewEndpoint(ChapterMailRepository mailRepo, ChapterRepository chapterRepo,
        AdministrativeDivisionRepository adminDivRepo, EmailService emailService, PermissionContext perms) {
        _mailRepo = mailRepo;
        _chapterRepo = chapterRepo;
        _adminDivRepo = adminDivRepo;
        _emailService = emailService;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/chapter-mail/{Id}/recipient-preview");
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

        var targetType = mail.TargetType == Api.ChapterMails.ChapterMailTargetType.Chapter ? "Chapter" : "AdministrativeDivision";
        var members = _emailService.FetchTargetMembers(targetType, mail.TargetId, mail.IncludeDescendants);

        var targetName = mail.TargetType == Api.ChapterMails.ChapterMailTargetType.Chapter
            ? _chapterRepo.Get(mail.TargetId)?.Name ?? ""
            : _adminDivRepo.Get(mail.TargetId)?.Name ?? "";
        var reasonPrefix = mail.TargetType == Api.ChapterMails.ChapterMailTargetType.Chapter
            ? "Mitglied der Gliederung"
            : "Mitglied im Gebiet";

        var recipients = members
            .Where(m => !string.IsNullOrEmpty(m.Email))
            .Select(m => new RecipientPreviewItem {
                FirstName = m.FirstName,
                LastName = m.LastName,
                Email = m.Email!,
                Reason = $"{reasonPrefix} {targetName}".Trim()
            })
            .ToList();

        await SendAsync(new RecipientPreviewResponse { Recipients = recipients }, cancellation: ct);
    }
}
