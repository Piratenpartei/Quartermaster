using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures.Endpoints.Offices;

public class OfficeImportEndpoint : Endpoint<OfficeImportRequest, OfficeImportResultDTO> {
    private readonly CustomOfficeImporter _importer;
    private readonly SignatureCampaignAccess _access;

    public OfficeImportEndpoint(CustomOfficeImporter importer, SignatureCampaignAccess access) {
        _importer = importer;
        _access = access;
    }

    public override void Configure() {
        Post("/api/support-signatures/offices/import");
    }

    public override async Task HandleAsync(OfficeImportRequest req, CancellationToken ct) {
        if (!_access.CanUseOffices()) {
            await SendForbiddenAsync(ct);
            return;
        }

        var (result, errorKey) = _importer.Run(req.Content, req.Apply);
        if (result == null) {
            AddError(errorKey!);
            await SendErrorsAsync(400, ct);
            return;
        }

        await SendAsync(result, cancellation: ct);
    }
}
