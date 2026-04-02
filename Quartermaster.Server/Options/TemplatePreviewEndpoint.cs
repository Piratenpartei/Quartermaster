using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Options;
using Quartermaster.Api.Rendering;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Options;

// TODO: Migrate to client-side rendering in OptionDetail.razor.cs — Blazor page currently calls this endpoint
public class TemplatePreviewEndpoint : Endpoint<TemplatePreviewRequest, TemplatePreviewResponse> {
    private readonly UserGlobalPermissionRepository _globalPermRepo;

    public TemplatePreviewEndpoint(UserGlobalPermissionRepository globalPermRepo) {
        _globalPermRepo = globalPermRepo;
    }

    public override void Configure() {
        Post("/api/options/preview");
    }

    public override async Task HandleAsync(TemplatePreviewRequest req, CancellationToken ct) {
        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!EndpointAuthorizationHelper.HasGlobalPermission(userId.Value, PermissionIdentifier.ViewOptions, _globalPermRepo)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var mockData = TemplateMockDataProvider.GetMockData(req.TemplateModels);
        var (html, error) = await TemplateRenderer.RenderAsync(req.TemplateText, mockData);

        await SendAsync(new TemplatePreviewResponse {
            RenderedHtml = html ?? "",
            Error = error
        }, cancellation: ct);
    }
}
