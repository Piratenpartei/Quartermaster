using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Data.NewsPosts;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.NewsPosts.Pipeline;

namespace Quartermaster.Server.NewsPosts.Endpoints;

public class NewsItemRetryEndpoint : Endpoint<NewsItemIdRequest> {
    private readonly NewsPipeline _pipeline;
    private readonly NewsItemRepository _itemRepo;
    private readonly PermissionContext _perms;

    public NewsItemRetryEndpoint(NewsPipeline pipeline, NewsItemRepository itemRepo, PermissionContext perms) {
        _pipeline = pipeline;
        _itemRepo = itemRepo;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/newsposts/items/{Id}/retry");
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
        if (_itemRepo.Get(req.Id) == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!_pipeline.ResetFailed(req.Id)) {
            await SendAsync(new { }, 409, ct);
            return;
        }

        await SendNoContentAsync(ct);
    }
}
