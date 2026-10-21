using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures.Endpoints.Campaigns;

public class CampaignOverviewEndpoint : Endpoint<SignatureCampaignIdRequest, SignatureOverviewDTO> {
    private readonly SignatureCampaignRepository _campaigns;
    private readonly SignatureOverviewBuilder _builder;
    private readonly SignatureCampaignAccess _access;

    public CampaignOverviewEndpoint(SignatureCampaignRepository campaigns, SignatureOverviewBuilder builder, SignatureCampaignAccess access) {
        _campaigns = campaigns;
        _builder = builder;
        _access = access;
    }

    public override void Configure() {
        Get("/api/support-signatures/campaigns/{Id}/overview");
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

        await SendAsync(_builder.Build(campaign, DateTime.UtcNow), cancellation: ct);
    }
}
