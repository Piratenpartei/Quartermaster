using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.NewsPosts;
using Quartermaster.Data.NewsPosts;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.NewsPosts.Endpoints;

public class NewsFeedSaveEndpoint : Endpoint<NewsFeedDTO, NewsFeedDTO> {
    private readonly NewsFeedRepository _feedRepo;
    private readonly PermissionContext _perms;

    public NewsFeedSaveEndpoint(NewsFeedRepository feedRepo, PermissionContext perms) {
        _feedRepo = feedRepo;
        _perms = perms;
    }

    public override void Configure() {
        Put("/api/newsposts/feeds");
    }

    public override async Task HandleAsync(NewsFeedDTO req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.ManageNewsPosts)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var error = Validate(req);
        if (error != null) {
            AddError(error);
            await SendErrorsAsync(400, ct);
            return;
        }

        NewsFeed feed;
        if (req.Id == Guid.Empty) {
            feed = new NewsFeed { Name = req.Name.Trim(), Url = req.Url.Trim(), Kind = (NewsFeedKind)req.Kind, IsEnabled = req.IsEnabled };
            _feedRepo.Create(feed);
        } else {
            var existing = _feedRepo.Get(req.Id);
            if (existing == null) {
                await SendNotFoundAsync(ct);
                return;
            }
            existing.Name = req.Name.Trim();
            existing.Url = req.Url.Trim();
            existing.Kind = (NewsFeedKind)req.Kind;
            existing.IsEnabled = req.IsEnabled;
            _feedRepo.Update(existing);
            feed = existing;
        }

        await SendAsync(feed.ToDto(), cancellation: ct);
    }

    private static string? Validate(NewsFeedDTO req) {
        if (string.IsNullOrWhiteSpace(req.Name))
            return "Bitte einen Namen angeben.";
        if (!Uri.TryCreate(req.Url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return "Bitte eine gültige http(s)-URL angeben.";
        if (req.Kind != 0 && req.Kind != 1)
            return "Ungültige Quellenart.";
        return null;
    }
}
