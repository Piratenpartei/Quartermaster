using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Chapters;
using Quartermaster.Data.Chapters;

namespace Quartermaster.Server.Chapters;

public class ChapterSearchEndpoint : Endpoint<ChapterSearchRequest, ChapterSearchResponse> {
    private readonly ChapterRepository _chapterRepo;

    public ChapterSearchEndpoint(ChapterRepository chapterRepo) {
        _chapterRepo = chapterRepo;
    }

    public override void Configure() {
        Get("/api/chapters/search");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ChapterSearchRequest req, CancellationToken ct) {
        var (items, totalCount) = _chapterRepo.Search(req.Query, req.Page, req.PageSize);

        await SendAsync(new ChapterSearchResponse {
            Items = items.Select(c => new ChapterDTO {
                Id = c.Id,
                Name = c.Name,
                ShortCode = c.ShortCode,
                AdministrativeDivisionId = c.AdministrativeDivisionId,
                ExternalCode = c.ExternalCode,
                ParentChapterId = c.ParentChapterId
            }).ToList(),
            TotalCount = totalCount
        }, cancellation: ct);
    }
}
