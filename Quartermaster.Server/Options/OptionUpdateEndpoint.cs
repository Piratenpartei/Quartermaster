using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Options;
using Quartermaster.Data.Options;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Options;

public class OptionUpdateEndpoint : Endpoint<OptionUpdateRequest> {
    private readonly OptionRepository _optionRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;

    public OptionUpdateEndpoint(OptionRepository optionRepo, UserGlobalPermissionRepository globalPermRepo) {
        _optionRepo = optionRepo;
        _globalPermRepo = globalPermRepo;
    }

    public override void Configure() {
        Post("/api/options");
    }

    public override async Task HandleAsync(OptionUpdateRequest req, CancellationToken ct) {
        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!EndpointAuthorizationHelper.HasGlobalPermission(userId.Value, PermissionIdentifier.EditOptions, _globalPermRepo)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var def = _optionRepo.GetDefinition(req.Identifier);
        if (def == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        if (req.ChapterId.HasValue && !def.IsOverridable) {
            await SendErrorsAsync(400, ct);
            return;
        }

        _optionRepo.SetValue(req.Identifier, req.ChapterId, req.Value);
        await SendOkAsync(ct);
    }
}
