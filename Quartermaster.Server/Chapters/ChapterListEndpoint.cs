using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Chapters;
using Quartermaster.Data.Chapters;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Chapters;

public class ChapterListEndpoint : EndpointWithoutRequest<List<ChapterDTO>> {
    private readonly ChapterRepository _chapterRepository;
    private readonly PermissionContext _perms;

    public ChapterListEndpoint(ChapterRepository chapterRepository, PermissionContext perms) {
        _chapterRepository = chapterRepository;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/chapters");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct) {
        var dtos = _chapterRepository.ToDtos(_chapterRepository.GetAll(), includeContactEmail: _perms.UserId != null);
        await SendAsync(dtos, cancellation: ct);
    }
}
