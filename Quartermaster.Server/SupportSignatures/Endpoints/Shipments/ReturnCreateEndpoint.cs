using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.SupportSignatures.Endpoints.Shipments;

public class ReturnCreateEndpoint : Endpoint<ReturnSaveRequest> {
    private readonly ShipmentCampaignLookup _lookup;
    private readonly SignatureShipmentService _service;
    private readonly SignatureCampaignAccess _access;
    private readonly PermissionContext _perms;

    public ReturnCreateEndpoint(ShipmentCampaignLookup lookup, SignatureShipmentService service, SignatureCampaignAccess access, PermissionContext perms) {
        _lookup = lookup;
        _service = service;
        _access = access;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/support-signatures/shipments/{Id}/returns");
    }

    public override async Task HandleAsync(ReturnSaveRequest req, CancellationToken ct) {
        var campaign = _lookup.ForShipment(req.Id);
        if (campaign == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!_access.CanManage(campaign)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var outcome = _service.RecordReturn(req.Id, req.ReturnedOn, req.Lines, _perms.UserId!.Value);
        if (!outcome.Succeeded) {
            AddError(outcome.ErrorKey!);
            await SendErrorsAsync(outcome.StatusCode, ct);
            return;
        }

        await SendOkAsync(ct);
    }
}
