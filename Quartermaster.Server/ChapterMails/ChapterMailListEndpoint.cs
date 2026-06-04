using System;
using System.Collections.Generic;
using System.Linq;
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

public class ChapterMailListRequest {
    [QueryParam]
    public bool IncludeArchived { get; set; }
}

public class ChapterMailListEndpoint : Endpoint<ChapterMailListRequest, ChapterMailListResponse> {
    private readonly ChapterMailRepository _mailRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly AdministrativeDivisionRepository _adminDivRepo;
    private readonly DbContext _db;
    private readonly PermissionContext _perms;

    public ChapterMailListEndpoint(ChapterMailRepository mailRepo, ChapterRepository chapterRepo,
        AdministrativeDivisionRepository adminDivRepo, DbContext db, PermissionContext perms) {
        _mailRepo = mailRepo;
        _chapterRepo = chapterRepo;
        _adminDivRepo = adminDivRepo;
        _db = db;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/chapter-mail");
    }

    public override async Task HandleAsync(ChapterMailListRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var permitted = _perms.GetPermittedChapterIds(PermissionIdentifier.ManageChapterMail);
        List<Guid> chapterIds;
        if (permitted == null) {
            chapterIds = _chapterRepo.GetAll().Select(c => c.Id).ToList();
        } else {
            if (permitted.Count == 0) {
                await SendForbiddenAsync(ct);
                return;
            }
            chapterIds = permitted;
        }

        var chapters = _chapterRepo.GetAll().ToDictionary(c => c.Id, c => c);
        var mails = _mailRepo.ListForChapters(chapterIds, req.IncludeArchived);

        var items = mails.Select(m => new ChapterMailListItemDTO {
            Id = m.Id,
            ChapterId = m.ChapterId,
            ChapterName = chapters.TryGetValue(m.ChapterId, out var ch) ? ch.Name : "",
            Title = m.Title,
            TargetType = m.TargetType,
            TargetName = ResolveTargetName(m.TargetType, m.TargetId, chapters),
            CreatedAt = new DateTimeOffset(m.CreatedAt, TimeSpan.Zero),
            SentAt = m.SentAt.HasValue ? new DateTimeOffset(m.SentAt.Value, TimeSpan.Zero) : null,
            RecipientCount = m.RecipientCount,
            DeletedAt = m.DeletedAt.HasValue ? new DateTimeOffset(m.DeletedAt.Value, TimeSpan.Zero) : null
        }).ToList();

        await SendAsync(new ChapterMailListResponse { Items = items }, cancellation: ct);
    }

    private string ResolveTargetName(ChapterMailTargetType type, Guid id, Dictionary<Guid, Chapter> chapters) {
        if (type == ChapterMailTargetType.Chapter)
            return chapters.TryGetValue(id, out var ch) ? ch.Name : "";
        if (type == ChapterMailTargetType.AdministrativeDivision)
            return _adminDivRepo.Get(id)?.Name ?? "";
        return "";
    }
}
