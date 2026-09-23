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

/// <summary>Restores a template to the body that ships with the build.</summary>
public class PdfTemplateResetEndpoint : Endpoint<PdfTemplateKeyRequest, PdfTemplateDTO> {
    private readonly PdfTemplateRepository _repo;
    private readonly PermissionContext _perms;

    public PdfTemplateResetEndpoint(PdfTemplateRepository repo, PermissionContext perms) {
        _repo = repo;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/pdf-templates/{Key}/reset");
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
        if (entry == null || !PdfTemplateDefaults.ByKey.TryGetValue(req.Key, out var defaultBody)) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (_repo.GetByKey(req.Key) == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var now = DateTime.UtcNow;
        _repo.UpdateBody(req.Key, defaultBody, now, _perms.UserId.Value);

        await SendAsync(new PdfTemplateDTO {
            Key = req.Key,
            DisplayNameI18nKey = entry.DisplayNameI18nKey,
            Body = defaultBody,
            PaletteModels = entry.PaletteModels,
            UpdatedAt = new DateTimeOffset(now, TimeSpan.Zero)
        }, cancellation: ct);
    }
}
