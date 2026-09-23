using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Forms;
using Quartermaster.Data.Forms.PdfTemplates;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Forms.PdfTemplating.Endpoints;

public class PdfTemplateSaveBody : PdfTemplateSaveRequest {
    public string Key { get; set; } = "";
}

public class PdfTemplateSaveEndpoint : Endpoint<PdfTemplateSaveBody> {
    private readonly PdfTemplateRepository _repo;
    private readonly PdfTemplateEngine _engine;
    private readonly PermissionContext _perms;

    public PdfTemplateSaveEndpoint(PdfTemplateRepository repo, PdfTemplateEngine engine, PermissionContext perms) {
        _repo = repo;
        _engine = engine;
        _perms = perms;
    }

    public override void Configure() {
        Put("/api/pdf-templates/{Key}");
    }

    public override async Task HandleAsync(PdfTemplateSaveBody req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.EditPdfTemplates)) {
            await SendForbiddenAsync(ct);
            return;
        }
        if (!FormCatalog.Entries.Any(e => e.FormKey == req.Key)) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (_repo.GetByKey(req.Key) == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        _repo.UpdateBody(req.Key, req.Body, DateTime.UtcNow, _perms.UserId.Value);
        await SendOkAsync(ct);
    }
}
