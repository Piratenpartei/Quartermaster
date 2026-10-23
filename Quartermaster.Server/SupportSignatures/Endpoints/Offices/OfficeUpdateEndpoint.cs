using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.I18n;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures.Endpoints.Offices;

public class OfficeUpdateEndpoint : Endpoint<CertificationOfficeSaveRequest, CertificationOfficeDTO> {
    private readonly CertificationOfficeWriter _writer;
    private readonly CertificationOfficeRepository _offices;
    private readonly SignatureCampaignAccess _access;

    public OfficeUpdateEndpoint(CertificationOfficeWriter writer, CertificationOfficeRepository offices, SignatureCampaignAccess access) {
        _writer = writer;
        _offices = offices;
        _access = access;
    }

    public override void Configure() {
        Put("/api/support-signatures/offices/{Id}");
    }

    public override async Task HandleAsync(CertificationOfficeSaveRequest req, CancellationToken ct) {
        if (!_access.CanUseOffices()) {
            await SendForbiddenAsync(ct);
            return;
        }
        var existing = _offices.Get(req.Id);
        if (existing == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (existing.IsOfficial) {
            AddError(I18nKey.Error.SupportSignatures.Office.Official);
            await SendErrorsAsync(409, ct);
            return;
        }

        var (office, status, errorKey) = _writer.Save(req, req.Id);
        if (office == null) {
            AddError(errorKey!);
            await SendErrorsAsync(status, ct);
            return;
        }

        await SendAsync(office.ToDto([], _offices.HasShipments(office.Id)), cancellation: ct);
    }
}
