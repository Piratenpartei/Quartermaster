using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Options;
using Quartermaster.Data.Options;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Options;

public class OptionUpdateEndpoint : Endpoint<OptionUpdateRequest> {
    private readonly OptionRepository _optionRepo;
    private readonly PermissionContext _perms;

    public OptionUpdateEndpoint(OptionRepository optionRepo, PermissionContext perms) {
        _optionRepo = optionRepo;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/options");
    }

    public override async Task HandleAsync(OptionUpdateRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.EditOptions)) {
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
