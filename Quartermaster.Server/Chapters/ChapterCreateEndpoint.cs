using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Chapters;
using Quartermaster.Data.Chapters;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Chapters;

/// <summary>
/// Creates a new chapter, optionally under a parent. Requires the global
/// <see cref="PermissionIdentifier.CreateChapter"/> permission. <c>ExternalCode</c>
/// must be unique under the parent (or under the root null parent for top-level chapters).
/// </summary>
public class ChapterCreateEndpoint : Endpoint<ChapterCreateRequest, ChapterDTO> {
    private readonly ChapterRepository _chapterRepo;
    private readonly PermissionContext _perms;

    public ChapterCreateEndpoint(ChapterRepository chapterRepo, PermissionContext perms) {
        _chapterRepo = chapterRepo;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/chapters");
    }

    public override async Task HandleAsync(ChapterCreateRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.CreateChapter)) {
            await SendForbiddenAsync(ct);
            return;
        }
        var name = req.Name?.Trim() ?? "";
        if (string.IsNullOrEmpty(name)) {
            AddError(r => r.Name, "Name ist erforderlich.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }
        if (req.ParentChapterId.HasValue) {
            var parent = _chapterRepo.Get(req.ParentChapterId.Value);
            if (parent == null) {
                AddError(r => r.ParentChapterId, "Übergeordnete Gliederung existiert nicht.");
                await SendErrorsAsync(cancellation: ct);
                return;
            }
        }
        var externalCode = string.IsNullOrWhiteSpace(req.ExternalCode) ? null : req.ExternalCode.Trim();
        if (externalCode != null) {
            var existing = _chapterRepo.GetByExternalCodeAndParent(externalCode, req.ParentChapterId);
            if (existing != null) {
                AddError(r => r.ExternalCode, "Externer Code ist unter dieser übergeordneten Gliederung bereits vergeben.");
                await SendErrorsAsync(cancellation: ct);
                return;
            }
        }

        var chapter = new Chapter {
            Id = Guid.NewGuid(),
            Name = name,
            ShortCode = string.IsNullOrWhiteSpace(req.ShortCode) ? null : req.ShortCode.Trim(),
            ExternalCode = externalCode,
            ParentChapterId = req.ParentChapterId,
            AdministrativeDivisionId = req.AdministrativeDivisionId
        };
        _chapterRepo.Create(chapter);

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
