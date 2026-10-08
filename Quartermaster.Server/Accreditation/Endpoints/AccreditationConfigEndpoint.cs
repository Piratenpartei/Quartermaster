using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Accreditation;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Accreditation.Endpoints;

public class AccreditationConfigEndpoint : EndpointWithoutRequest<AccreditationConfigDTO> {
    private readonly PermissionContext _perms;
    private readonly KeycloakSettings _settings;

    public AccreditationConfigEndpoint(PermissionContext perms, KeycloakSettings settings) {
        _perms = perms;
        _settings = settings;
    }

    public override void Configure() {
        Get("/api/accreditation/config");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.ManageAccreditation)) {
            await SendForbiddenAsync(ct);
            return;
        }

        await SendAsync(new AccreditationConfigDTO {
            UsernameSeparator = _settings.UsernameSeparator,
            KeycloakConfigured = _settings.IsConfigured
        }, cancellation: ct);
    }
}
