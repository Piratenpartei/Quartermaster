using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Data.Templates;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Templates;

public class TemplateDeleteRequest {
    public Guid Id { get; set; }
}

public class TemplateDeleteEndpoint : Endpoint<TemplateDeleteRequest> {
    private readonly TemplateRepository _templateRepo;
    private readonly PermissionContext _perms;

    public TemplateDeleteEndpoint(TemplateRepository templateRepo, PermissionContext perms) {
        _templateRepo = templateRepo;
        _perms = perms;
    }

    public override void Configure() {
        Delete("/api/templates/{Id}");
    }

    public override async Task HandleAsync(TemplateDeleteRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        var template = _templateRepo.Get(req.Id);
        if (template == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (template.IsSystem && template.ChapterId == null) {
            await SendErrorsAsync(400, ct);
            return;
        }
        if (!CanEdit(template)) {
            await SendForbiddenAsync(ct);
            return;
        }

        _templateRepo.SoftDelete(template.Id);
        await SendOkAsync(ct);
    }

    private bool CanEdit(Template template) {
        if (template.ChapterId == null)
            return _perms.HasGlobal(PermissionIdentifier.EditTemplates);
        return _perms.Has(template.ChapterId.Value, PermissionIdentifier.EditTemplates);
    }
}
