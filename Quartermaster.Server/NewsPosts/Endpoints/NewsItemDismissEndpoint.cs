using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Data.NewsPosts;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.NewsPosts.Endpoints;

public class NewsItemDismissEndpoint : Endpoint<NewsItemIdRequest> {
    private readonly NewsItemRepository _itemRepo;
    private readonly PermissionContext _perms;

    public NewsItemDismissEndpoint(NewsItemRepository itemRepo, PermissionContext perms) {
        _itemRepo = itemRepo;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/newsposts/items/{Id}/dismiss");
    }

    public override async Task HandleAsync(NewsItemIdRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.ReviewNewsPosts)) {
            await SendForbiddenAsync(ct);
            return;
        }
        var item = _itemRepo.Get(req.Id);
        if (item == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (item.Status == NewsItemStatus.Posted || item.Status == NewsItemStatus.Posting) {
            await SendAsync(new { }, 409, ct);
            return;
        }

        _itemRepo.SetStatus(req.Id, NewsItemStatus.Dismissed);
        await SendNoContentAsync(ct);
    }
}
