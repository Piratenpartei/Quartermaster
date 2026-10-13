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

public class AssemblyMotionTypeUpdateEndpoint : Endpoint<AssemblyMotionTypeUpdateRequest, AssemblyMotionTypeSettingsDTO> {
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly EventRepository _events;
    private readonly AssemblyMotionAccess _access;

    public AssemblyMotionTypeUpdateEndpoint(AssemblyMotionPortalRepository portals, EventRepository events, AssemblyMotionAccess access) {
        _portals = portals;
        _events = events;
        _access = access;
    }

    public override void Configure() {
        Put("/api/assemblymotions/types/{Id}");
    }

    public override async Task HandleAsync(AssemblyMotionTypeUpdateRequest req, CancellationToken ct) {
        var type = _portals.GetType(req.Id);
        var portal = type == null ? null : _portals.Get(type.PortalId);
        var ev = portal == null ? null : _events.Get(portal.EventId);
        if (type == null || portal == null || ev == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!_access.CanEditPortal(ev)) {
            await SendForbiddenAsync(ct);
            return;
        }

        req.Name = AssemblyMotionContent.SingleLineSetting(req.Name)!;
        var code = req.Code.Trim();
        var hasMotions = _portals.TypeHasMotions(type.Id);
        var codeChanged = !string.Equals(type.Code, code, StringComparison.Ordinal);
        if (codeChanged && hasMotions) {
            AddError(r => r.Code, I18nKey.Error.AssemblyMotion.Type.HasMotions);
            await SendErrorsAsync(409, ct);
            return;
        }

        var codeTaken = codeChanged && _portals.GetTypes(portal.Id)
            .Any(t => t.Id != type.Id && string.Equals(t.Code, code, StringComparison.OrdinalIgnoreCase));
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

        type.ApplyFrom(req);
        type.Code = code;
        _portals.UpdateType(type);

        await SendAsync(type.ToSettingsDto(hasMotions), cancellation: ct);
    }
}
