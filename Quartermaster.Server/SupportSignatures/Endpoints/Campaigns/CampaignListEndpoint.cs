using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures.Endpoints.Campaigns;

public class CampaignListEndpoint : Endpoint<SignatureCampaignListRequest, List<SignatureCampaignDTO>> {
    private readonly SignatureCampaignRepository _campaigns;
    private readonly SignatureCampaignDtoBuilder _builder;
    private readonly SignatureCampaignAccess _access;

    public CampaignListEndpoint(SignatureCampaignRepository campaigns, SignatureCampaignDtoBuilder builder, SignatureCampaignAccess access) {
        _campaigns = campaigns;
        _builder = builder;
        _access = access;
    }

    public override void Configure() {
        Get("/api/support-signatures/campaigns");
    }

    public override async Task HandleAsync(SignatureCampaignListRequest req, CancellationToken ct) {
        if (!_access.CanUseOffices()) {
            await SendForbiddenAsync(ct);
            return;
        }

        var campaigns = _campaigns.List(_access.ManageableChapterIds(), req.Archived);
        await SendAsync(_builder.BuildMany(campaigns), cancellation: ct);
    }
}
