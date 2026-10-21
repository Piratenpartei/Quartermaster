using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures.Endpoints.Campaigns;

public class CampaignFormDeleteEndpoint : Endpoint<SignatureCampaignIdRequest> {
    private readonly SignatureCampaignRepository _campaigns;
    private readonly SupportSignatureFormStorage _storage;
    private readonly SignatureCampaignAccess _access;
    private readonly AuditLogRepository _auditLog;

    public CampaignFormDeleteEndpoint(SignatureCampaignRepository campaigns, SupportSignatureFormStorage storage, SignatureCampaignAccess access,
        AuditLogRepository auditLog) {
        _campaigns = campaigns;
        _storage = storage;
        _access = access;
        _auditLog = auditLog;
    }

    public override void Configure() {
        Delete("/api/support-signatures/campaigns/{Id}/form");
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

        _campaigns.SetForm(campaign.Id, null);
        _storage.Delete(campaign.Id);
        _auditLog.Log("SignatureCampaign", campaign.Id, "FormDeleted", "FormFileName", campaign.FormFileName, null);
        await SendNoContentAsync(ct);
    }
}
