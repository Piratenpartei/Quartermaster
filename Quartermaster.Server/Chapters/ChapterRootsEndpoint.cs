using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Chapters;
using Quartermaster.Data.Chapters;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Chapters;

public class ChapterRootsEndpoint : EndpointWithoutRequest<List<ChapterDTO>> {
    private readonly ChapterRepository _chapterRepo;
    private readonly PermissionContext _perms;

    public ChapterRootsEndpoint(ChapterRepository chapterRepo, PermissionContext perms) {
        _chapterRepo = chapterRepo;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/chapters/roots");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct) {
        var dtos = _chapterRepo.ToDtos(_chapterRepo.GetRoots(), includeMemberFields: _perms.UserId != null);
        await SendAsync(dtos, cancellation: ct);
    }
}
