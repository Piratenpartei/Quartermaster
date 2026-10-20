using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Forwarding;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Forwarding;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Forwarding.Endpoints;

public class RedmineConnectionListEndpoint : EndpointWithoutRequest<List<RedmineConnectionDTO>> {
    private readonly RedmineConnectionRepository _connections;
    private readonly ChapterRepository _chapters;
    private readonly PermissionContext _perms;

    public RedmineConnectionListEndpoint(RedmineConnectionRepository connections, ChapterRepository chapters, PermissionContext perms) {
        _connections = connections;
        _chapters = chapters;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/forwarding/redmine-connections");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        if (!_perms.HasGlobal(PermissionIdentifier.EditOptions)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var list = _connections.List()
            .Select(c => new RedmineConnectionDTO {
                ChapterId = c.ChapterId,
                ChapterName = _chapters.Get(c.ChapterId)?.Name ?? "",
                BaseUrl = c.BaseUrl,
                HasApiKey = c.ApiKey.Length > 0,
                UpdatedAt = c.UpdatedAt.ToDtoUtc()
            })
            .OrderBy(c => c.ChapterName)
            .ToList();
        await SendAsync(list, cancellation: ct);
    }
}
