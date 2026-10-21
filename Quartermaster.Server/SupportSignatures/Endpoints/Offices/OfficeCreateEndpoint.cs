using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures.Endpoints.Offices;

public class OfficeCreateEndpoint : Endpoint<CertificationOfficeSaveRequest, CertificationOfficeDTO> {
    private readonly CertificationOfficeWriter _writer;
    private readonly SignatureCampaignAccess _access;

    public OfficeCreateEndpoint(CertificationOfficeWriter writer, SignatureCampaignAccess access) {
        _writer = writer;
        _access = access;
    }

    public override void Configure() {
        Post("/api/support-signatures/offices");
    }

    public override async Task HandleAsync(CertificationOfficeSaveRequest req, CancellationToken ct) {
        if (!_access.CanUseOffices()) {
            await SendForbiddenAsync(ct);
            return;
        }

        var (office, status, errorKey) = _writer.Save(req, null);
        if (office == null) {
            AddError(errorKey!);
            await SendErrorsAsync(status, ct);
            return;
        }

        await SendAsync(office.ToDto([], false), cancellation: ct);
    }
}
