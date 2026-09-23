using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Data.NewsPosts;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.NewsPosts.Endpoints;

public class NewsFeedDeleteEndpoint : EndpointWithoutRequest {
    private readonly NewsFeedRepository _feedRepo;
    private readonly NewsItemRepository _itemRepo;
    private readonly PermissionContext _perms;

    public NewsFeedDeleteEndpoint(NewsFeedRepository feedRepo, NewsItemRepository itemRepo, PermissionContext perms) {
        _feedRepo = feedRepo;
        _itemRepo = itemRepo;
        _perms = perms;
    }

    public override void Configure() {
        Delete("/api/newsposts/feeds/{Id}");
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
        var id = Route<Guid>("Id");
        if (_itemRepo.HasItems(id)) {
            AddError("Diese Quelle hat bereits Meldungen. Deaktiviere sie stattdessen.");
            await SendErrorsAsync(409, ct);
            return;
        }

        _feedRepo.Delete(id);
        await SendNoContentAsync(ct);
    }
}
