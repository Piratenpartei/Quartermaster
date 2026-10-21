using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data.SupportSignatures;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.SupportSignatures.Endpoints.Shipments;

public class ShipmentCreateSingleEndpoint : Endpoint<SingleShipmentRequest, ShipmentPrepareResponse> {
    private readonly SignatureCampaignRepository _campaigns;
    private readonly SignatureShipmentService _service;
    private readonly SignatureCampaignAccess _access;
    private readonly PermissionContext _perms;

    public ShipmentCreateSingleEndpoint(SignatureCampaignRepository campaigns, SignatureShipmentService service, SignatureCampaignAccess access,
        PermissionContext perms) {
        _campaigns = campaigns;
        _service = service;
        _access = access;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/support-signatures/campaigns/{Id}/shipments");
    }

    public override async Task HandleAsync(SingleShipmentRequest req, CancellationToken ct) {
        var campaign = _campaigns.Get(req.Id);
        if (campaign == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!_access.CanManage(campaign)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var lines = req.Lines.Select(l => new PlannedLine(l.MunicipalityId, l.Count)).ToList();
        var (id, outcome) = _service.CreateSingle(campaign.Id, req.OfficeId, lines, _perms.UserId!.Value);
        if (!outcome.Succeeded) {
            AddError(outcome.ErrorKey!);
            await SendErrorsAsync(outcome.StatusCode, ct);
            return;
        }

        await SendAsync(new ShipmentPrepareResponse { ShipmentIds = [id!.Value] }, cancellation: ct);
    }
}
