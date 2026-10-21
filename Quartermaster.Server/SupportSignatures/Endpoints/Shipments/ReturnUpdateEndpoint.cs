using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures.Endpoints.Shipments;

public class ReturnUpdateEndpoint : Endpoint<ReturnSaveRequest> {
    private readonly ShipmentCampaignLookup _lookup;
    private readonly SignatureShipmentService _service;
    private readonly SignatureCampaignAccess _access;

    public ReturnUpdateEndpoint(ShipmentCampaignLookup lookup, SignatureShipmentService service, SignatureCampaignAccess access) {
        _lookup = lookup;
        _service = service;
        _access = access;
    }

    public override void Configure() {
        Put("/api/support-signatures/returns/{Id}");
    }

    public override async Task HandleAsync(ReturnSaveRequest req, CancellationToken ct) {
        var campaign = _lookup.ForReturn(req.Id);
        if (campaign == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!_access.CanManage(campaign)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var outcome = _service.UpdateReturn(req.Id, req.ReturnedOn, req.Lines);
        if (!outcome.Succeeded) {
            AddError(outcome.ErrorKey!);
            await SendErrorsAsync(outcome.StatusCode, ct);
            return;
        }

        await SendOkAsync(ct);
    }
}
