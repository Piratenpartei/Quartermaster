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
        var dtos = chapters.Select(c => new ChapterDTO {
            Id = c.Id,
            Name = c.Name,
            ShortCode = c.ShortCode,
            AdministrativeDivisionId = c.AdministrativeDivisionId,
            ExternalCode = c.ExternalCode,
            ParentChapterId = c.ParentChapterId
        }).ToList();
        await SendAsync(dtos, cancellation: ct);
    }
}
