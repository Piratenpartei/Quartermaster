using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Portal;

public class AssemblyMotionTypeCreateEndpoint : Endpoint<AssemblyMotionTypeCreateRequest, AssemblyMotionTypeSettingsDTO> {
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly EventRepository _events;
    private readonly AssemblyMotionAccess _access;

    public AssemblyMotionTypeCreateEndpoint(AssemblyMotionPortalRepository portals, EventRepository events, AssemblyMotionAccess access) {
        _portals = portals;
        _events = events;
        _access = access;
    }

    public override void Configure() {
        Post("/api/assemblymotions/portals/{PortalId}/types");
    }

    public override async Task HandleAsync(AssemblyMotionTypeCreateRequest req, CancellationToken ct) {
        var portal = _portals.Get(req.PortalId);
        var ev = portal == null ? null : _events.Get(portal.EventId);
        if (portal == null || ev == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!_access.CanEditPortal(ev)) {
            await SendForbiddenAsync(ct);
            return;
        }

        req.Name = AssemblyMotionContent.SingleLineSetting(req.Name)!;
        var code = req.Code.Trim();
        var codeTaken = _portals.GetTypes(portal.Id)
            .Any(t => string.Equals(t.Code, code, StringComparison.OrdinalIgnoreCase));
        if (codeTaken) {
            AddError(r => r.Code, I18nKey.Error.AssemblyMotion.Type.CodeTaken);
            await SendErrorsAsync(409, ct);
            return;
        }

        if (!AssemblyMotionTypeRules.CompetingDeadlineIsValid(req, portal)) {
            AddError(r => r.CompetingDeadline, I18nKey.Error.AssemblyMotion.Type.CompetingDeadlineInvalid);
            await SendErrorsAsync(400, ct);
            return;
        }

        var type = new AssemblyMotionType {
            PortalId = portal.Id
        };
        type.ApplyFrom(req);
        type.Code = code;
        _portals.CreateType(type);

        await SendAsync(type.ToSettingsDto(false), cancellation: ct);
    }
}
