using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.I18n;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures.Endpoints.Offices;

public class OfficeDeleteEndpoint : Endpoint<CertificationOfficeIdRequest> {
    private readonly CertificationOfficeRepository _offices;
    private readonly AuditLogRepository _auditLog;
    private readonly SignatureCampaignAccess _access;

    public OfficeDeleteEndpoint(CertificationOfficeRepository offices, AuditLogRepository auditLog, SignatureCampaignAccess access) {
        _offices = offices;
        _auditLog = auditLog;
        _access = access;
    }

    public override void Configure() {
        Delete("/api/support-signatures/offices/{Id}");
    }

    public override async Task HandleAsync(CertificationOfficeIdRequest req, CancellationToken ct) {
        if (!_access.CanUseOffices()) {
            await SendForbiddenAsync(ct);
            return;
        }
        if (_offices.Get(req.Id) == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (_offices.HasShipments(req.Id)) {
            AddError(I18nKey.Error.SupportSignatures.Office.HasShipments);
            await SendErrorsAsync(409, ct);
            return;
        }

        _offices.Delete(req.Id);
        _auditLog.Log("CertificationOffice", req.Id, "Deleted");
        await SendNoContentAsync(ct);
    }
}
