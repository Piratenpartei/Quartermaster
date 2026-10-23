using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures.Endpoints.Offices;

/// <summary>The office forms of a municipality go to, hand-entered or official, with the municipalities it covers.</summary>
public class OfficeResponsibleEndpoint : Endpoint<OfficeResponsibleRequest, CertificationOfficeDTO> {
    private readonly CertificationOfficeRepository _offices;
    private readonly AdministrativeDivisionRepository _divisions;
    private readonly SignatureCampaignAccess _access;

    public OfficeResponsibleEndpoint(CertificationOfficeRepository offices, AdministrativeDivisionRepository divisions, SignatureCampaignAccess access) {
        _offices = offices;
        _divisions = divisions;
        _access = access;
    }

    public override void Configure() {
        Get("/api/support-signatures/offices/responsible");
    }

    public override async Task HandleAsync(OfficeResponsibleRequest req, CancellationToken ct) {
        if (!_access.CanUseOffices()) {
            await SendForbiddenAsync(ct);
            return;
        }

        var officeId = _offices.GetOfficeIdsByMunicipality([req.MunicipalityId]).GetValueOrDefault(req.MunicipalityId);
        var office = _offices.Get(officeId);
        if (office == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var municipalityIds = office.IsOfficial ? _offices.GetOfficialMunicipalityIds(office.Id) : _offices.GetMunicipalityIds(office.Id);
        var municipalities = _divisions.GetMany(municipalityIds)
            .Select(d => new OfficeMunicipalityDTO { Id = d.Id, Name = d.Name })
            .OrderBy(m => m.Name)
            .ToList();
        await SendAsync(office.ToDto(municipalities, _offices.HasShipments(office.Id)), cancellation: ct);
    }
}
