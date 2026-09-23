using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.Extensions.Logging;
using Quartermaster.Api;
using Quartermaster.Api.NewsPosts;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.NewsPosts.Postiz;

namespace Quartermaster.Server.NewsPosts.Endpoints;

public class NewsChannelListEndpoint : EndpointWithoutRequest<List<PostizChannelDTO>> {
    private readonly IPostizClient _postiz;
    private readonly NewsPostsSettings _settings;
    private readonly PermissionContext _perms;

    public NewsChannelListEndpoint(IPostizClient postiz, NewsPostsSettings settings, PermissionContext perms) {
        _postiz = postiz;
        _settings = settings;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/newsposts/channels");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.ReviewNewsPosts)) {
            await SendForbiddenAsync(ct);
            return;
        }
        if (!_settings.HasPostiz) {
            await SendAsync([], 503, ct);
            return;
        }

        try {
            var channels = await _postiz.GetChannelsAsync(ct);
            await SendAsync(channels
                .Select(c => new PostizChannelDTO { Id = c.Id, Name = c.Name, Platform = c.Identifier, Profile = c.Profile, Picture = c.Picture, Disabled = c.Disabled })
                .ToList(), cancellation: ct);
        } catch (PostizException ex) {
            Logger.LogWarning(ex, "Loading Postiz channels failed");
            await SendAsync([], 502, ct);
        } catch (HttpRequestException ex) {
            Logger.LogWarning(ex, "Loading Postiz channels failed");
            await SendAsync([], 502, ct);
        }
    }
}
