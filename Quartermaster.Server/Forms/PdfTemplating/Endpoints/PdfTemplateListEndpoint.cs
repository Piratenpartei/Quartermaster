using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Forms;
using Quartermaster.Data.Forms.PdfTemplates;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Forms.PdfTemplating.Endpoints;

public class PdfTemplateListEndpoint : EndpointWithoutRequest<List<PdfTemplateListItemDTO>> {
    private readonly PdfTemplateRepository _repo;
    private readonly PermissionContext _perms;

    public PdfTemplateListEndpoint(PdfTemplateRepository repo, PermissionContext perms) {
        _repo = repo;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/pdf-templates");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.EditPdfTemplates)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var byKey = _repo.GetAll().ToDictionary(t => t.Key);
        var items = FormCatalog.Entries
            .Select(entry => new PdfTemplateListItemDTO {
                Key = entry.FormKey,
                DisplayNameI18nKey = entry.DisplayNameI18nKey,
                UpdatedAt = byKey.TryGetValue(entry.FormKey, out var t)
                    ? new DateTimeOffset(DateTime.SpecifyKind(t.UpdatedAt, DateTimeKind.Utc))
                    : default
            })
            .ToList();

        await SendAsync(items, cancellation: ct);
    }
}
