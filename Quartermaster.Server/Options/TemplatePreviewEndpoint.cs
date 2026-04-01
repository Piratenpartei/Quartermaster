using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Options;
using Quartermaster.Api.Rendering;

namespace Quartermaster.Server.Options;

// TODO: Migrate to client-side rendering in OptionDetail.razor.cs — Blazor page currently calls this endpoint
public class TemplatePreviewEndpoint : Endpoint<TemplatePreviewRequest, TemplatePreviewResponse> {
    public override void Configure() {
        Post("/api/options/preview");
        AllowAnonymous();
    }

    public override async Task HandleAsync(TemplatePreviewRequest req, CancellationToken ct) {
        var mockData = TemplateMockDataProvider.GetMockData(req.TemplateModels);
        var (html, error) = await TemplateRenderer.RenderAsync(req.TemplateText, mockData);

        await SendAsync(new TemplatePreviewResponse {
            RenderedHtml = html ?? "",
            Error = error
        }, cancellation: ct);
    }
}
