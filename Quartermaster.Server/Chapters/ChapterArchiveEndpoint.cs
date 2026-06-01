using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Data.Chapters;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Chapters;

/// <summary>Archives a chapter — see <see cref="ChapterRepository.SetArchived"/> for what that means.</summary>
public class ChapterArchiveEndpoint : EndpointWithoutRequest {
    private readonly ChapterRepository _chapterRepo;
    private readonly PermissionContext _perms;

    public ChapterArchiveEndpoint(ChapterRepository chapterRepo, PermissionContext perms) {
        _chapterRepo = chapterRepo;
        _perms = perms;
    }

    public override void Configure() {
        Put("/api/chapters/{Id}/archive");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.ArchiveChapter)) {
            await SendForbiddenAsync(ct);
            return;
        }
        var id = Route<Guid>("Id");
        var chapter = _chapterRepo.Get(id);
        if (chapter == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        _chapterRepo.SetArchived(id, archived: true);
        await SendNoContentAsync(ct);
    }
}
