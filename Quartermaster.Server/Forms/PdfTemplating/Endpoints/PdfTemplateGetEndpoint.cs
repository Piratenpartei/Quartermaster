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

public class PdfTemplateKeyRequest {
    public string Key { get; set; } = "";
}

public class PdfTemplateGetEndpoint : Endpoint<PdfTemplateKeyRequest, PdfTemplateDTO> {
    private readonly PdfTemplateRepository _repo;
    private readonly PermissionContext _perms;

    public PdfTemplateGetEndpoint(PdfTemplateRepository repo, PermissionContext perms) {
        _repo = repo;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/pdf-templates/{Key}");
    }

    public override async Task HandleAsync(PdfTemplateKeyRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.EditPdfTemplates)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var entry = FormCatalog.Entries.FirstOrDefault(e => e.FormKey == req.Key);
        if (entry == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        var template = _repo.GetByKey(req.Key);
        if (template == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        await SendAsync(new PdfTemplateDTO {
            Key = template.Key,
            DisplayNameI18nKey = entry.DisplayNameI18nKey,
            Body = template.Body,
            PaletteModels = entry.PaletteModels,
            UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(template.UpdatedAt, DateTimeKind.Utc))
        }, cancellation: ct);
    }
}
