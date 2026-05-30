using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Quartermaster.Api;
using Quartermaster.Data.Templates;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Templates;

public class TemplatePdfRequest {
    public Guid Id { get; set; }
}

public class TemplatePdfEndpoint : Endpoint<TemplatePdfRequest> {
    private readonly TemplateRepository _templateRepo;
    private readonly PermissionContext _perms;

    public TemplatePdfEndpoint(TemplateRepository templateRepo, PermissionContext perms) {
        _templateRepo = templateRepo;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/templates/{Id}/pdf");
    }

    public override async Task HandleAsync(TemplatePdfRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        var template = _templateRepo.Get(req.Id);
        if (template == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!CanView(template)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var (pdf, error) = await TemplatePdfRenderer.RenderAsync(template);
        if (error != null || pdf == null) {
            await SendStringAsync(error ?? "PDF render failed", 500, cancellation: ct);
            return;
        }

        HttpContext.Response.Headers.ContentDisposition = $"inline; filename=\"template-{template.Id}.pdf\"";
        await SendBytesAsync(pdf, contentType: "application/pdf", cancellation: ct);
    }

    private bool CanView(Template template) {
        if (template.ChapterId == null)
            return _perms.HasGlobal(PermissionIdentifier.ViewTemplates);
        return _perms.Has(template.ChapterId.Value, PermissionIdentifier.ViewTemplates);
    }
}
