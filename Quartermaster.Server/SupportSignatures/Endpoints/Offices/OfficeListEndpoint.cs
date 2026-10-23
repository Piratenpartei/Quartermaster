using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures.Endpoints.Offices;

public class OfficeListEndpoint : EndpointWithoutRequest<List<CertificationOfficeDTO>> {
    private readonly CertificationOfficeRepository _offices;
    private readonly AdministrativeDivisionRepository _divisions;
    private readonly SignatureCampaignAccess _access;

    public OfficeListEndpoint(CertificationOfficeRepository offices, AdministrativeDivisionRepository divisions, SignatureCampaignAccess access) {
        _offices = offices;
        _divisions = divisions;
        _access = access;
    }

    public override void Configure() {
        Get("/api/support-signatures/offices");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        if (!_access.CanUseOffices()) {
            await SendForbiddenAsync(ct);
            return;
        }

        var municipalityIds = _offices.GetMunicipalityIdsByOffice();
        var names = _divisions.GetMany(municipalityIds.Values.SelectMany(ids => ids))
            .ToDictionary(d => d.Id, d => d.Name);
        var withShipments = _offices.GetOfficeIdsWithShipments();

        var offices = _offices.ListCustom()
            .Select(o => o.ToDto(
                municipalityIds.GetValueOrDefault(o.Id, [])
                    .Select(id => new OfficeMunicipalityDTO { Id = id, Name = names.GetValueOrDefault(id, "") })
                    .OrderBy(m => m.Name)
                    .ToList(),
                withShipments.Contains(o.Id)))
            .ToList();
        await SendAsync(offices, cancellation: ct);
    }
}
