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

public class ChapterMailCreateResponse {
    public Guid Id { get; set; }
}

public class ChapterMailCreateEndpoint : Endpoint<ChapterMailCreateRequest, ChapterMailCreateResponse> {
    private readonly ChapterMailRepository _mailRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly AdministrativeDivisionRepository _adminDivRepo;
    private readonly DbContext _db;
    private readonly PermissionContext _perms;

    public ChapterMailCreateEndpoint(ChapterMailRepository mailRepo, ChapterRepository chapterRepo,
        AdministrativeDivisionRepository adminDivRepo, DbContext db, PermissionContext perms) {
        _mailRepo = mailRepo;
        _chapterRepo = chapterRepo;
        _adminDivRepo = adminDivRepo;
        _db = db;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/chapter-mail");
    }

    public override async Task HandleAsync(ChapterMailCreateRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.Has(req.ChapterId, PermissionIdentifier.ManageChapterMail)) {
            await SendForbiddenAsync(ct);
            return;
        }
        if (string.IsNullOrWhiteSpace(req.Title)) {
            await SendErrorsAsync(400, ct);
            return;
        }

        var targetError = ChapterMailTargetValidator.Validate(req.ChapterId, req.TargetType, req.TargetId,
            _chapterRepo, _adminDivRepo, _db);
        if (targetError != null) {
            AddError(targetError);
            await SendErrorsAsync(400, ct);
            return;
        }

        var mail = new ChapterMail {
            ChapterId = req.ChapterId,
            Title = req.Title.Trim(),
            Subject = string.IsNullOrWhiteSpace(req.Subject) ? null : req.Subject.Trim(),
            TargetType = req.TargetType,
            TargetId = req.TargetId,
            IncludeDescendants = req.IncludeDescendants,
            TemplateId = req.TemplateId,
            Content = req.Content,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = _perms.UserId.Value
        };

        _mailRepo.Create(mail);
        await SendAsync(new ChapterMailCreateResponse { Id = mail.Id }, cancellation: ct);
    }
}
