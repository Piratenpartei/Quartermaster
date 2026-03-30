using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Options;

namespace Quartermaster.Server.Options;

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
