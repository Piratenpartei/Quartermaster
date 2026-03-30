using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Chapters;
using Quartermaster.Data.Chapters;

namespace Quartermaster.Server.Chapters;

public class ChapterRootsEndpoint : EndpointWithoutRequest<List<ChapterDTO>> {
    private readonly ChapterRepository _chapterRepo;

    public ChapterRootsEndpoint(ChapterRepository chapterRepo) {
        _chapterRepo = chapterRepo;
    }

    public override void Configure() {
        Get("/api/chapters/roots");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct) {
        var roots = _chapterRepo.GetRoots();
        await SendAsync(roots.Select(c => c.ToDto()).ToList(), cancellation: ct);
    }
}
