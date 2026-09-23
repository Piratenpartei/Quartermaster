using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.NewsPosts;
using Quartermaster.Data.NewsPosts;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.NewsPosts.Endpoints;

public class NewsFeedListEndpoint : EndpointWithoutRequest<List<NewsFeedDTO>> {
    private readonly NewsFeedRepository _feedRepo;
    private readonly PermissionContext _perms;

    public NewsFeedListEndpoint(NewsFeedRepository feedRepo, PermissionContext perms) {
        _feedRepo = feedRepo;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/newsposts/feeds");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.ManageNewsPosts)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var feeds = _feedRepo.GetAll().Select(feed => feed.ToDto()).ToList();

        await SendAsync(feeds, cancellation: ct);
    }
}
