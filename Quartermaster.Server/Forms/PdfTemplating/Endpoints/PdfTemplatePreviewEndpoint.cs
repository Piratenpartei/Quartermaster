using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Forms;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.Forms;

namespace Quartermaster.Server.Forms.PdfTemplating.Endpoints;

public class PdfTemplatePreviewBody : PdfTemplatePreviewRequest {
    public string Key { get; set; } = "";
}

/// <summary>Renders the supplied (unsaved) template body against the form's mock data and streams the PDF for live preview. Parse/render errors come back as 400 with the message so the editor can surface them inline.</summary>
public class PdfTemplatePreviewEndpoint : Endpoint<PdfTemplatePreviewBody> {
    private readonly PdfTemplateEngine _engine;
    private readonly FormAssets _assets;
    private readonly IEnumerable<IFormMockData> _mockData;
    private readonly PermissionContext _perms;

    public PdfTemplatePreviewEndpoint(PdfTemplateEngine engine, FormAssets assets,
        IEnumerable<IFormMockData> mockData, PermissionContext perms) {
        _engine = engine;
        _assets = assets;
        _mockData = mockData;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/pdf-templates/{Key}/preview");
    }

    public override async Task HandleAsync(PdfTemplatePreviewBody req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.EditPdfTemplates)) {
            await SendForbiddenAsync(ct);
            return;
        }
        var mock = _mockData.FirstOrDefault(m => m.FormKey == req.Key);
        if (mock == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        try {
            var pdf = await _engine.RenderAsync(req.Body, mock.BuildModel(), _assets.Resolve);
            await SendBytesAsync(pdf, fileName: "vorschau.pdf", contentType: "application/pdf", cancellation: ct);
        } catch (PdfTemplateParseException ex) {
            AddError(ex.Message);
            await SendErrorsAsync(400, ct);
        }
    }
}
