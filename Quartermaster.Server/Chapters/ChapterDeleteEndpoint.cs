using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.I18n;
using Quartermaster.Data.Chapters;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Chapters;

/// <summary>
/// Soft-deletes a chapter (sets <c>DeletedAt</c>). Requires the global
/// <see cref="PermissionIdentifier.DeleteChapter"/> permission and refuses when
/// the chapter has at least one non-deleted child chapter.
/// </summary>
public class ChapterDeleteEndpoint : EndpointWithoutRequest {
    private readonly ChapterRepository _chapterRepo;
    private readonly PermissionContext _perms;

    public ChapterDeleteEndpoint(ChapterRepository chapterRepo, PermissionContext perms) {
        _chapterRepo = chapterRepo;
        _perms = perms;
    }

    public override void Configure() {
        Delete("/api/chapters/{Id}");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.DeleteChapter)) {
            await SendForbiddenAsync(ct);
            return;
        }
        var id = Route<Guid>("Id");
        var chapter = _chapterRepo.Get(id);
        if (chapter == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (_chapterRepo.HasNonDeletedChildren(id)) {
            AddError(I18nKey.Error.Chapter.HasChildren);
            await SendErrorsAsync(cancellation: ct);
            return;
        }
        _chapterRepo.SoftDelete(id);
        await SendNoContentAsync(ct);
    }
}
