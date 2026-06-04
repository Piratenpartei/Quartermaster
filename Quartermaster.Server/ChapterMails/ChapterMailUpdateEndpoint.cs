using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.ChapterMails;
using Quartermaster.Data;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.ChapterMails;
using Quartermaster.Data.Chapters;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.ChapterMails;

public class ChapterMailUpdateBoundRequest : ChapterMailUpdateRequest {
    public Guid Id { get; set; }
}

public class ChapterMailUpdateEndpoint : Endpoint<ChapterMailUpdateBoundRequest> {
    private readonly ChapterMailRepository _mailRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly AdministrativeDivisionRepository _adminDivRepo;
    private readonly DbContext _db;
    private readonly PermissionContext _perms;

    public ChapterMailUpdateEndpoint(ChapterMailRepository mailRepo, ChapterRepository chapterRepo,
        AdministrativeDivisionRepository adminDivRepo, DbContext db, PermissionContext perms) {
        _mailRepo = mailRepo;
        _chapterRepo = chapterRepo;
        _adminDivRepo = adminDivRepo;
        _db = db;
        _perms = perms;
    }

    public override void Configure() {
        Put("/api/chapter-mail/{Id}");
    }

    public override async Task HandleAsync(ChapterMailUpdateBoundRequest req, CancellationToken ct) {
        var existing = _mailRepo.Get(req.Id);
        if (existing == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.Has(existing.ChapterId, PermissionIdentifier.ManageChapterMail)) {
            await SendForbiddenAsync(ct);
            return;
        }
        if (existing.IsSent || existing.IsArchived) {
            AddError("mail is sent or archived and can no longer be edited");
            await SendErrorsAsync(409, ct);
            return;
        }
        if (string.IsNullOrWhiteSpace(req.Title)) {
            await SendErrorsAsync(400, ct);
            return;
        }

        var targetError = ChapterMailTargetValidator.Validate(existing.ChapterId, req.TargetType, req.TargetId,
            _chapterRepo, _adminDivRepo, _db);
        if (targetError != null) {
            AddError(targetError);
            await SendErrorsAsync(400, ct);
            return;
        }

        var original = new ChapterMail {
            Id = existing.Id,
            Title = existing.Title,
            Subject = existing.Subject,
            TargetType = existing.TargetType,
            TargetId = existing.TargetId,
            IncludeDescendants = existing.IncludeDescendants,
            TemplateId = existing.TemplateId,
            Content = existing.Content
        };
        var updated = new ChapterMail {
            Id = existing.Id,
            Title = req.Title.Trim(),
            Subject = string.IsNullOrWhiteSpace(req.Subject) ? null : req.Subject.Trim(),
            TargetType = req.TargetType,
            TargetId = req.TargetId,
            IncludeDescendants = req.IncludeDescendants,
            TemplateId = req.TemplateId,
            Content = req.Content
        };

        _mailRepo.Update(updated, original);
        await SendOkAsync(ct);
    }
}
