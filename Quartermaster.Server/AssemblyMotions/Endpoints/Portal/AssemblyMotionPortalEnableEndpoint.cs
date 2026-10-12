using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Events;
using Quartermaster.Server.AssemblyMotions.Wiki;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Portal;

public class AssemblyMotionPortalEnableEndpoint : Endpoint<AssemblyMotionPortalEnableRequest, AssemblyMotionPortalSettingsDTO> {
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly EventRepository _events;
    private readonly AssemblyMotionAccess _access;
    private readonly AssemblyMotionPortalSettingsBuilder _builder;

    public AssemblyMotionPortalEnableEndpoint(AssemblyMotionPortalRepository portals, EventRepository events,
        AssemblyMotionAccess access, AssemblyMotionPortalSettingsBuilder builder) {
        _portals = portals;
        _events = events;
        _access = access;
        _builder = builder;
    }

    public override void Configure() {
        Post("/api/assemblymotions/portals");
    }

    public override async Task HandleAsync(AssemblyMotionPortalEnableRequest req, CancellationToken ct) {
        var ev = _events.Get(req.EventId);
        if (ev == null) {
            AddError(I18nKey.Error.AssemblyMotion.Portal.EventNotFound);
            await SendErrorsAsync(404, ct);
            return;
        }
        if (!_access.CanEditPortal(ev)) {
            await SendForbiddenAsync(ct);
            return;
        }
        if (_portals.GetByEvent(ev.Id) != null) {
            AddError(I18nKey.Error.AssemblyMotion.Portal.AlreadyExists);
            await SendErrorsAsync(409, ct);
            return;
        }

        var portal = _portals.Create(ev.Id, req.OpensAt.UtcDateTime, req.ClosesAt.UtcDateTime, DateTime.UtcNow, AssemblyMotionWikiTemplates.DefaultFor);
        await SendAsync(_builder.Build(portal, ev), cancellation: ct);
    }
}
