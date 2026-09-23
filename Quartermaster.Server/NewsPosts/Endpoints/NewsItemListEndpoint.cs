using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.NewsPosts;
using Quartermaster.Data.NewsPosts;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.NewsPosts.Endpoints;

public class NewsItemListRequest {
    [QueryParam]
    public string View { get; set; } = "suggestions";

    [QueryParam]
    public int? Status { get; set; }
}

public class NewsItemListEndpoint : Endpoint<NewsItemListRequest, List<NewsItemDTO>> {
    private const int Limit = 200;

    private readonly NewsItemRepository _itemRepo;
    private readonly ProgrammeRepository _programmeRepo;
    private readonly NewsFeedRepository _feedRepo;
    private readonly PermissionContext _perms;

    public NewsItemListEndpoint(NewsItemRepository itemRepo, ProgrammeRepository programmeRepo, NewsFeedRepository feedRepo, PermissionContext perms) {
        _itemRepo = itemRepo;
        _programmeRepo = programmeRepo;
        _feedRepo = feedRepo;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/newsposts/items");
    }

    public override async Task HandleAsync(NewsItemListRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.ReviewNewsPosts)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var items = _itemRepo.GetByStatuses(StatusesFor(req), Limit);
        await SendAsync(NewsItemMapper.ToDtos(items, _itemRepo, _programmeRepo, _feedRepo), cancellation: ct);
    }

    private static List<NewsItemStatus> StatusesFor(NewsItemListRequest req) {
        if (req.Status != null)
            return [(NewsItemStatus)req.Status.Value];

        return req.View switch {
            "posted" => [NewsItemStatus.Posted],
            "all" => [
                NewsItemStatus.New, NewsItemStatus.Irrelevant, NewsItemStatus.Relevant, NewsItemStatus.Generated,
                NewsItemStatus.Posting, NewsItemStatus.Posted, NewsItemStatus.Dismissed, NewsItemStatus.Failed
            ],
            _ => [NewsItemStatus.Relevant, NewsItemStatus.Generated]
        };
    }
}
