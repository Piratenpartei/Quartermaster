using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Chapters;
using Quartermaster.Data.Chapters;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Chapters;

public class ChapterSearchEndpoint : Endpoint<ChapterSearchRequest, ChapterSearchResponse> {
    private readonly ChapterRepository _chapterRepo;
    private readonly PermissionContext _perms;

    public ChapterSearchEndpoint(ChapterRepository chapterRepo, PermissionContext perms) {
        _chapterRepo = chapterRepo;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/chapters/search");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ChapterSearchRequest req, CancellationToken ct) {
        var (items, totalCount) = _chapterRepo.Search(req.Query, req.Page, req.PageSize);

        await SendAsync(new ChapterSearchResponse {
            Items = _chapterRepo.ToDtos(items, includeContactEmail: _perms.UserId != null),
            TotalCount = totalCount,
            Page = req.Page,
            PageSize = req.PageSize
        }, cancellation: ct);
    }
}
