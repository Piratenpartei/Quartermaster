using System;
using Quartermaster.Data.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures.Endpoints.Shipments;

/// <summary>Finds the campaign behind a shipment or return so the endpoints can check access before the service runs.</summary>
public class ShipmentCampaignLookup {
    private readonly SignatureShipmentRepository _shipments;
    private readonly SignatureCampaignRepository _campaigns;

    public ShipmentCampaignLookup(SignatureShipmentRepository shipments, SignatureCampaignRepository campaigns) {
        _shipments = shipments;
        _campaigns = campaigns;
    }

    public SignatureCampaign? ForShipment(Guid shipmentId) {
        var shipment = _shipments.Get(shipmentId);
        return shipment == null ? null : _campaigns.Get(shipment.CampaignId);
    }

    public SignatureCampaign? ForReturn(Guid returnId) {
        var signatureReturn = _shipments.GetReturn(returnId);
        return signatureReturn == null ? null : ForShipment(signatureReturn.ShipmentId);
    }
}
