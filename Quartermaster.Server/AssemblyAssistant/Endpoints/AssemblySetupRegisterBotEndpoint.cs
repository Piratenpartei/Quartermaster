using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Server.AssemblyAssistant.Mumble;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.AssemblyAssistant.Endpoints;

public class AssemblySetupRegisterBotEndpoint : EndpointWithoutRequest<AssemblySetupRegisterDTO> {
    private readonly PermissionContext _perms;
    private readonly MumbleConnectionCheck _check;

    public AssemblySetupRegisterBotEndpoint(PermissionContext perms, MumbleConnectionCheck check) {
        _perms = perms;
        _check = check;
    }

    public override void Configure() {
        Post("/api/assembly-assistant/setup/register");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        var status = AssemblySetupGuard.Check(_perms);
        if (status != null) {
            await SendErrorsAsync(status.Value, ct);
            return;
        }

        await SendAsync(await _check.RegisterAsync(ct), cancellation: ct);
    }
}
