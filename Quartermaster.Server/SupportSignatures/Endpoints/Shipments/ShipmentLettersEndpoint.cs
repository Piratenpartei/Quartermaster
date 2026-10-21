using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.Extensions.Logging;
using Quartermaster.Data.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures.Endpoints.Shipments;

public class ShipmentLettersRequest {
    /// <summary>The campaign; bound from the route.</summary>
    public Guid Id { get; set; }

    /// <summary>The shipments to print, as repeated <c>ids</c> query parameters.</summary>
    public List<Guid> Ids { get; set; } = [];
}

public class ShipmentLettersEndpoint : Endpoint<ShipmentLettersRequest> {
    private readonly SignatureCampaignRepository _campaigns;
    private readonly SignatureShipmentRepository _shipments;
    private readonly CoverLetterRenderer _renderer;
    private readonly SignatureCampaignAccess _access;

    public ShipmentLettersEndpoint(SignatureCampaignRepository campaigns, SignatureShipmentRepository shipments, CoverLetterRenderer renderer,
        SignatureCampaignAccess access) {
        _campaigns = campaigns;
        _shipments = shipments;
        _renderer = renderer;
        _access = access;
    }

    public override void Configure() {
        Get("/api/support-signatures/campaigns/{Id}/shipments/letters");
    }

    public override async Task HandleAsync(ShipmentLettersRequest req, CancellationToken ct) {
        var campaign = _campaigns.Get(req.Id);
        if (campaign == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!_access.CanManage(campaign)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var ids = _shipments.List(campaign.Id)
            .Select(s => s.Id)
            .Intersect(req.Ids)
            .ToList();
        if (ids.Count == 0) {
            await SendNotFoundAsync(ct);
            return;
        }

        var (pdf, error) = await _renderer.RenderAsync(campaign, ids, ct);
        if (pdf == null) {
            Logger.LogWarning("Cover letters for campaign {CampaignId} could not be rendered: {Error}", campaign.Id, error);
            ThrowError(error ?? "");
            return;
        }

        await SendBytesAsync(pdf, $"Anschreiben-{campaign.Name}.pdf", "application/pdf", cancellation: ct);
    }
}
