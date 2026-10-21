using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures.Endpoints.Shipments;

public class ShipmentListEndpoint : Endpoint<SignatureCampaignIdRequest, List<SignatureShipmentDTO>> {
    private readonly SignatureCampaignRepository _campaigns;
    private readonly SignatureShipmentService _service;
    private readonly SignatureCampaignAccess _access;

    public ShipmentListEndpoint(SignatureCampaignRepository campaigns, SignatureShipmentService service, SignatureCampaignAccess access) {
        _campaigns = campaigns;
        _service = service;
        _access = access;
    }

    public override void Configure() {
        Get("/api/support-signatures/campaigns/{Id}/shipments");
    }

    public override async Task HandleAsync(SignatureCampaignIdRequest req, CancellationToken ct) {
        var campaign = _campaigns.Get(req.Id);
        if (campaign == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!_access.CanManage(campaign)) {
            await SendForbiddenAsync(ct);
            return;
        }

        await SendAsync(_service.List(campaign.Id, DateOnly.FromDateTime(DateTime.UtcNow)), cancellation: ct);
    }
}
