using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures.Endpoints.Shipments;

public class ShipmentPlanEndpoint : Endpoint<SignatureCampaignIdRequest, ShipmentPlanDTO> {
    private readonly SignatureCampaignRepository _campaigns;
    private readonly SignatureShipmentService _service;
    private readonly CertificationOfficeRepository _offices;
    private readonly AdministrativeDivisionRepository _divisions;
    private readonly SignatureCampaignAccess _access;

    public ShipmentPlanEndpoint(SignatureCampaignRepository campaigns, SignatureShipmentService service, CertificationOfficeRepository offices,
        AdministrativeDivisionRepository divisions, SignatureCampaignAccess access) {
        _campaigns = campaigns;
        _service = service;
        _offices = offices;
        _divisions = divisions;
        _access = access;
    }

    public override void Configure() {
        Get("/api/support-signatures/campaigns/{Id}/shipment-plan");
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

        var plan = _service.Plan(campaign.Id);
        var names = _divisions.GetMany(plan.Offices.SelectMany(o => o.Lines).Concat(plan.Unassigned).Select(l => l.MunicipalityId))
            .ToDictionary(d => d.Id, d => d.Name);
        var offices = _offices.GetMany(plan.Offices.Select(o => o.OfficeId)).ToDictionary(o => o.Id);

        await SendAsync(new ShipmentPlanDTO {
            Offices = plan.Offices
                .Select(o => new ShipmentPlanOfficeDTO {
                    OfficeId = o.OfficeId,
                    OfficeName = offices.GetValueOrDefault(o.OfficeId)?.Name ?? "",
                    IsOfficial = offices.GetValueOrDefault(o.OfficeId)?.IsOfficial == true,
                    Lines = o.Lines.Select(l => Line(l, names)).OrderBy(l => l.MunicipalityName).ToList()
                })
                .OrderBy(o => o.OfficeName)
                .ToList(),
            Unassigned = plan.Unassigned.Select(l => Line(l, names)).OrderBy(l => l.MunicipalityName).ToList()
        }, cancellation: ct);
    }

    private static PlanLineDTO Line(PlannedLine line, Dictionary<Guid, string> names) {
        return new PlanLineDTO { MunicipalityId = line.MunicipalityId, MunicipalityName = names.GetValueOrDefault(line.MunicipalityId, ""), Count = line.Count };
    }
}
