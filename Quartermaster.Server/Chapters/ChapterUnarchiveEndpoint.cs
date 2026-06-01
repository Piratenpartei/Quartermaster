using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Data.Chapters;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Chapters;

/// <summary>Reverse of <see cref="ChapterArchiveEndpoint"/> — see <see cref="ChapterRepository.SetArchived"/>.</summary>
public class ChapterUnarchiveEndpoint : EndpointWithoutRequest {
    private readonly ChapterRepository _chapterRepo;
    private readonly PermissionContext _perms;

    public ChapterUnarchiveEndpoint(ChapterRepository chapterRepo, PermissionContext perms) {
        _chapterRepo = chapterRepo;
        _perms = perms;
    }

    public override void Configure() {
        Put("/api/chapters/{Id}/unarchive");
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
        _chapterRepo.SetArchived(id, archived: false);
        await SendNoContentAsync(ct);
    }
}
