using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Forwarding;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.Forwarding.SageEntry;

namespace Quartermaster.Server.Forwarding.Endpoints;

/// <summary>Lists what is still missing before the SAGE entry check can run, so the application form can be gated on it.</summary>
public class SageSetupStatusEndpoint : EndpointWithoutRequest<SageSetupStatusDTO> {
    private readonly SageEntrySetup _setup;
    private readonly PermissionContext _perms;

    public SageSetupStatusEndpoint(SageEntrySetup setup, PermissionContext perms) {
        _setup = setup;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/forwarding/sage-setup");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        if (!_perms.HasGlobal(PermissionIdentifier.EditOptions)) {
            await SendForbiddenAsync(ct);
            return;
        }

        await SendOkAsync(new SageSetupStatusDTO { Missing = _setup.Missing() }, ct);
    }
}
