using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures.Endpoints.Campaigns;

public class CampaignArchiveEndpoint : Endpoint<SignatureCampaignIdRequest, SignatureCampaignDTO> {
    private readonly SignatureCampaignRepository _campaigns;
    private readonly SignatureCampaignDtoBuilder _builder;
    private readonly SignatureCampaignAccess _access;
    private readonly AuditLogRepository _auditLog;

    public CampaignArchiveEndpoint(SignatureCampaignRepository campaigns, SignatureCampaignDtoBuilder builder, SignatureCampaignAccess access,
        AuditLogRepository auditLog) {
        _campaigns = campaigns;
        _builder = builder;
        _access = access;
        _auditLog = auditLog;
    }

    public override void Configure() {
        Post("/api/support-signatures/campaigns/{Id}/archive");
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

        campaign.ArchivedAt ??= DateTime.UtcNow;
        _campaigns.SetArchivedAt(campaign.Id, campaign.ArchivedAt);
        _auditLog.Log("SignatureCampaign", campaign.Id, "Archived");

        await SendAsync(_builder.Build(campaign), cancellation: ct);
    }
}
