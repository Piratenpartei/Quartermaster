using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Templates;
using Quartermaster.Data.Templates;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Templates;

public class TemplateListEndpoint : EndpointWithoutRequest<List<TemplateListItemDTO>> {
    private readonly TemplateRepository _templateRepo;
    private readonly PermissionContext _perms;

    public TemplateListEndpoint(TemplateRepository templateRepo, PermissionContext perms) {
        _templateRepo = templateRepo;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/templates");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var permitted = _perms.GetPermittedChapterIds(PermissionIdentifier.ViewTemplates);
        if (permitted != null && permitted.Count == 0) {
            await SendForbiddenAsync(ct);
            return;
        }

        var allTemplates = _templateRepo.GetAll();
        var permittedSet = permitted?.ToHashSet();

        var dtos = allTemplates
            .Where(t => t.ChapterId == null
                || permittedSet == null
                || permittedSet.Contains(t.ChapterId.Value))
            .Select(t => new TemplateListItemDTO {
                Id = t.Id,
                Identifier = t.Identifier,
                DisplayName = t.DisplayName,
                IsSystem = t.IsSystem,
                ChapterId = t.ChapterId,
                AllowsMemberFields = t.AllowsMemberFields,
                AllowsEventFields = t.AllowsEventFields,
                AllowsChapterFields = t.AllowsChapterFields
            })
            .ToList();

        await SendAsync(dtos, cancellation: ct);
    }
}
