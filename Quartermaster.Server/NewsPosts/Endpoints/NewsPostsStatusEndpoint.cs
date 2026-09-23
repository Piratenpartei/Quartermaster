using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.Extensions.Logging;
using Quartermaster.Api;
using Quartermaster.Api.NewsPosts;
using Quartermaster.Data.NewsPosts;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.NewsPosts.Pipeline;
using Quartermaster.Server.NewsPosts.Postiz;
using Quartermaster.Server.NewsPosts.Programme;

namespace Quartermaster.Server.NewsPosts.Endpoints;

public class NewsPostsStatusEndpoint : EndpointWithoutRequest<NewsPostsStatusDTO> {
    private readonly NewsPostsSettings _settings;
    private readonly IPostizClient _postiz;
    private readonly ProgrammeIndex _index;
    private readonly NewsItemRepository _itemRepo;
    private readonly PermissionContext _perms;

    public NewsPostsStatusEndpoint(NewsPostsSettings settings, IPostizClient postiz, ProgrammeIndex index, NewsItemRepository itemRepo, PermissionContext perms) {
        _settings = settings;
        _postiz = postiz;
        _index = index;
        _itemRepo = itemRepo;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/newsposts/status");
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

        var status = new NewsPostsStatusDTO {
            Enabled = _settings.Enabled,
            MistralConfigured = _settings.HasMistral,
            PostizConfigured = _settings.HasPostiz,
            LastRunAt = NewsPipelineHostedService.LastRunAt == null
                ? null
                : new DateTimeOffset(DateTime.SpecifyKind(NewsPipelineHostedService.LastRunAt.Value, DateTimeKind.Utc)),
            LastRunError = NewsPipelineHostedService.LastRunError,
            IndexedSections = _index.Count,
            ItemsByStatus = _itemRepo.CountByStatus().ToDictionary(kv => kv.Key.ToString(), kv => kv.Value)
        };

        if (_settings.HasPostiz) {
            try {
                await _postiz.GetChannelsAsync(ct);
                status.PostizReachable = true;
            } catch (PostizException ex) {
                Logger.LogWarning(ex, "Postiz reachability check failed");
            } catch (HttpRequestException ex) {
                Logger.LogWarning(ex, "Postiz reachability check failed");
            }
        }

        await SendAsync(status, cancellation: ct);
    }
}
