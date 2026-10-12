using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Events;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Commission;

public class AssemblyMotionBulkResultEndpoint : Endpoint<AssemblyMotionBulkResultRequest> {
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly EventRepository _events;
    private readonly AssemblyMotionAccess _access;
    private readonly AssemblyMotionResultService _service;
    private readonly PermissionContext _perms;

    public AssemblyMotionBulkResultEndpoint(AssemblyMotionPortalRepository portals, EventRepository events,
        AssemblyMotionAccess access, AssemblyMotionResultService service, PermissionContext perms) {
        _portals = portals;
        _events = events;
        _access = access;
        _service = service;
        _perms = perms;
    }

    public override void Configure() {
        Put("/api/assemblymotions/portals/{PortalId}/results");
    }

    public override async Task HandleAsync(AssemblyMotionBulkResultRequest req, CancellationToken ct) {
        var portal = _portals.Get(req.PortalId);
        var ev = portal == null ? null : _events.Get(portal.EventId);
        if (portal == null || ev == null || !AssemblyMotionEventVisibility.IsVisible(ev, _perms)) {
            await SendNotFoundAsync(ct);
            return;
        }

        if (!_access.IsCommission(ev)) {
            AddError(I18nKey.Error.AssemblyMotion.NotCommission);
            await SendErrorsAsync(403, ct);
            return;
        }

        var outcome = _service.SetResults(portal.Id, req.Items, DateTime.UtcNow);
        if (!outcome.Succeeded) {
            AddError(outcome.ErrorKey!);
            await SendErrorsAsync(outcome.StatusCode, ct);
            return;
        }

        await SendNoContentAsync(ct);
    }
}
