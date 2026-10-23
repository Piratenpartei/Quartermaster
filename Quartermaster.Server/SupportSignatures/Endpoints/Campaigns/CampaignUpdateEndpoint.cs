using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures.Endpoints.Campaigns;

public class CampaignUpdateEndpoint : Endpoint<SignatureCampaignSaveRequest, SignatureCampaignDTO> {
    private readonly SignatureCampaignRepository _campaigns;
    private readonly SignatureCampaignDtoBuilder _builder;
    private readonly SignatureCampaignAccess _access;
    private readonly AuditLogRepository _auditLog;

    public CampaignUpdateEndpoint(SignatureCampaignRepository campaigns, SignatureCampaignDtoBuilder builder, SignatureCampaignAccess access,
        AuditLogRepository auditLog) {
        _campaigns = campaigns;
        _builder = builder;
        _access = access;
        _auditLog = auditLog;
    }

    public override void Configure() {
        Put("/api/support-signatures/campaigns/{Id}");
    }

    public override async Task HandleAsync(SignatureCampaignSaveRequest req, CancellationToken ct) {
        var campaign = _campaigns.Get(req.Id);
        if (campaign == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!_access.CanManage(campaign)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var before = AuditText(campaign);
        SignatureCampaignSettings.Apply(campaign, req);
        _campaigns.UpdateSettings(campaign);
        var after = AuditText(campaign);
        _auditLog.Log("SignatureCampaign", campaign.Id, "Updated", "Settings", before, after);

        await SendAsync(_builder.Build(campaign), cancellation: ct);
    }

    private static string AuditText(SignatureCampaign campaign) {
        return $"{campaign.Name}|{campaign.Target}|{campaign.MarginPercent}|{campaign.Deadline:o}|{campaign.OverdueDays}|{campaign.Visibility}|{campaign.PostalAddress}|{campaign.ContactEmail}";
    }
}
