using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.Extensions.Logging;
using Quartermaster.Api;
using Quartermaster.Data.NewsPosts;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.NewsPosts.Mistral;
using Quartermaster.Server.NewsPosts.Pipeline;

namespace Quartermaster.Server.NewsPosts.Endpoints;

public class NewsItemIdRequest {
    public Guid Id { get; set; }
}

/// <summary>Maps both <see cref="MistralException"/> and <see cref="HttpRequestException"/> to 502 so a manual generation surfaces the model/network error to the reviewer instead of a generic failure.</summary>
public class NewsItemGenerateEndpoint : Endpoint<NewsItemIdRequest> {
    private readonly NewsPipeline _pipeline;
    private readonly NewsItemRepository _itemRepo;
    private readonly PermissionContext _perms;

    public NewsItemGenerateEndpoint(NewsPipeline pipeline, NewsItemRepository itemRepo, PermissionContext perms) {
        _pipeline = pipeline;
        _itemRepo = itemRepo;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/newsposts/items/{Id}/generate");
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

        try {
            if (!await _pipeline.GenerateForItemAsync(req.Id, ct)) {
                await SendAsync(new { }, 409, ct);
                return;
            }
        } catch (MistralException ex) {
            Logger.LogWarning(ex, "Manual generation for {Id} failed", req.Id);
            AddError(ex.Message);
            await SendErrorsAsync(502, ct);
            return;
        } catch (HttpRequestException ex) {
            Logger.LogWarning(ex, "Manual generation for {Id} failed", req.Id);
            AddError(ex.Message);
            await SendErrorsAsync(502, ct);
            return;
        }
        await SendNoContentAsync(ct);
    }
}
