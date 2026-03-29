using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Chapters;
using Quartermaster.Data.Chapters;

namespace Quartermaster.Server.Chapters;

public class ChapterListEndpoint : EndpointWithoutRequest<List<ChapterDTO>> {
    private readonly ChapterRepository _chapterRepository;

    public ChapterListEndpoint(ChapterRepository chapterRepository) {
        _chapterRepository = chapterRepository;
    }

    public override void Configure() {
        Get("/api/chapters");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct) {
        var chapters = _chapterRepository.GetAll();
        await SendAsync(chapters.Select(c => c.ToDto()).ToList(), cancellation: ct);
    }
}
