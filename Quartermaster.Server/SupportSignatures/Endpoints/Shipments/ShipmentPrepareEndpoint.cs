using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data.SupportSignatures;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.SupportSignatures.Endpoints.Shipments;

public class ShipmentPrepareEndpoint : Endpoint<ShipmentPrepareRequest, ShipmentPrepareResponse> {
    private readonly SignatureCampaignRepository _campaigns;
    private readonly SignatureShipmentService _service;
    private readonly SignatureCampaignAccess _access;
    private readonly PermissionContext _perms;

    public ShipmentPrepareEndpoint(SignatureCampaignRepository campaigns, SignatureShipmentService service, SignatureCampaignAccess access, PermissionContext perms) {
        _campaigns = campaigns;
        _service = service;
        _access = access;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/support-signatures/campaigns/{Id}/shipments/prepare");
    }

    public override async Task HandleAsync(ShipmentPrepareRequest req, CancellationToken ct) {
        var campaign = _campaigns.Get(req.Id);
        if (campaign == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!_access.CanManage(campaign)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var (ids, outcome) = _service.Prepare(campaign.Id, req.OfficeIds, _perms.UserId!.Value);
        if (!outcome.Succeeded) {
            AddError(outcome.ErrorKey!);
            await SendErrorsAsync(outcome.StatusCode, ct);
            return;
        }

        await SendAsync(new ShipmentPrepareResponse { ShipmentIds = ids }, cancellation: ct);
    }
}
