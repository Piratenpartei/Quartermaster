using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures.Endpoints.Shipments;

public class ShipmentConfirmEndpoint : Endpoint<ShipmentConfirmRequest> {
    private readonly ShipmentCampaignLookup _lookup;
    private readonly SignatureShipmentService _service;
    private readonly SignatureCampaignAccess _access;

    public ShipmentConfirmEndpoint(ShipmentCampaignLookup lookup, SignatureShipmentService service, SignatureCampaignAccess access) {
        _lookup = lookup;
        _service = service;
        _access = access;
    }

    public override void Configure() {
        Post("/api/support-signatures/shipments/{Id}/confirm");
    }

    public override async Task HandleAsync(ShipmentConfirmRequest req, CancellationToken ct) {
        var campaign = _lookup.ForShipment(req.Id);
        if (campaign == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!_access.CanManage(campaign)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var outcome = _service.Confirm(req.Id, req.SentOn);
        if (!outcome.Succeeded) {
            AddError(outcome.ErrorKey!);
            await SendErrorsAsync(outcome.StatusCode, ct);
            return;
        }

        await SendOkAsync(ct);
    }
}
