using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Templates;
using Quartermaster.Data.Templates;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Templates;

public class TemplateUpdateEndpoint : Endpoint<TemplateUpdateRequest> {
    private readonly TemplateRepository _templateRepo;
    private readonly PermissionContext _perms;

    public TemplateUpdateEndpoint(TemplateRepository templateRepo, PermissionContext perms) {
        _templateRepo = templateRepo;
        _perms = perms;
    }

    public override void Configure() {
        Put("/api/templates/{Id}");
    }

    public override async Task HandleAsync(TemplateUpdateRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        var template = _templateRepo.Get(req.Id);
        if (template == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!CanEdit(template)) {
            await SendForbiddenAsync(ct);
            return;
        }
        if (string.IsNullOrWhiteSpace(req.DisplayName)) {
            await SendErrorsAsync(400, ct);
            return;
        }

        template.DisplayName = req.DisplayName.Trim();
        template.Subject = req.Subject;
        template.Body = req.Body;
        template.AllowsMemberFields = req.AllowsMemberFields;
        template.AllowsEventFields = req.AllowsEventFields;
        template.AllowsChapterFields = req.AllowsChapterFields;
        _templateRepo.Update(template);

        await SendOkAsync(ct);
    }

    private bool CanEdit(Template template) {
        if (template.ChapterId == null)
            return _perms.HasGlobal(PermissionIdentifier.EditTemplates);
        return _perms.Has(template.ChapterId.Value, PermissionIdentifier.EditTemplates);
    }
}
