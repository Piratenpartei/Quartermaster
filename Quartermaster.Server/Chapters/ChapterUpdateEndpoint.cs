using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.I18n;
using Quartermaster.Data.Chapters;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Chapters;

/// <summary>
/// Updates an existing chapter. Requires the global <see cref="PermissionIdentifier.EditChapter"/>
/// permission. Cannot set a chapter as its own ancestor (no cycle detection at depth — the
/// shallow self-reference check is enough for the current UX where re-parenting happens
/// one step at a time).
/// </summary>
public class ChapterUpdateEndpoint : Endpoint<ChapterUpdateEndpoint.Request, ChapterDTO> {
    public class Request : ChapterUpdateRequest {
        public Guid Id { get; set; }
    }

    private readonly ChapterRepository _chapterRepo;
    private readonly PermissionContext _perms;

    public ChapterUpdateEndpoint(ChapterRepository chapterRepo, PermissionContext perms) {
        _chapterRepo = chapterRepo;
        _perms = perms;
    }

    public override void Configure() {
        Put("/api/chapters/{Id}");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.EditChapter)) {
            await SendForbiddenAsync(ct);
            return;
        }
        var chapter = _chapterRepo.Get(req.Id);
        if (chapter == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        var name = req.Name?.Trim() ?? "";
        if (string.IsNullOrEmpty(name)) {
            AddError(r => r.Name, I18nKey.Error.Chapter.NameRequired);
            await SendErrorsAsync(cancellation: ct);
            return;
        }
        if (req.ParentChapterId == req.Id) {
            AddError(r => r.ParentChapterId, I18nKey.Error.Chapter.ParentSelfReference);
            await SendErrorsAsync(cancellation: ct);
            return;
        }
        if (req.ParentChapterId.HasValue) {
            var parent = _chapterRepo.Get(req.ParentChapterId.Value);
            if (parent == null) {
                AddError(r => r.ParentChapterId, I18nKey.Error.Chapter.ParentNotFound);
                await SendErrorsAsync(cancellation: ct);
                return;
            }
        }
        var externalCode = string.IsNullOrWhiteSpace(req.ExternalCode) ? null : req.ExternalCode.Trim();
        if (externalCode != null) {
            var existing = _chapterRepo.GetByExternalCodeAndParent(externalCode, req.ParentChapterId);
            if (existing != null && existing.Id != req.Id) {
                AddError(r => r.ExternalCode, I18nKey.Error.Chapter.ExternalCodeNotUnique);
                await SendErrorsAsync(cancellation: ct);
                return;
            }
        }

        chapter.Name = name;
        chapter.ShortCode = string.IsNullOrWhiteSpace(req.ShortCode) ? null : req.ShortCode.Trim();
        chapter.ExternalCode = externalCode;
        chapter.ParentChapterId = req.ParentChapterId;
        chapter.AdministrativeDivisionId = req.AdministrativeDivisionId;
        _chapterRepo.Update(chapter);

        await SendAsync(new ChapterDTO {
            Id = chapter.Id,
            Name = chapter.Name,
            ShortCode = chapter.ShortCode,
            ExternalCode = chapter.ExternalCode,
            ParentChapterId = chapter.ParentChapterId,
            AdministrativeDivisionId = chapter.AdministrativeDivisionId
        }, cancellation: ct);
    }
}
