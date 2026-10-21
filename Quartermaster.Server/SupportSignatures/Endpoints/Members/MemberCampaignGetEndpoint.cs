using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures.Endpoints.Members;

public class MemberCampaignGetEndpoint : Endpoint<SignatureCampaignIdRequest, MemberSignatureCampaignDTO> {
    private readonly MemberSignatureCampaigns _campaigns;

    public MemberCampaignGetEndpoint(MemberSignatureCampaigns campaigns) {
        _campaigns = campaigns;
    }

    public override void Configure() {
        Get("/api/support-signatures/member-campaigns/{Id}");
    }

    public override async Task HandleAsync(SignatureCampaignIdRequest req, CancellationToken ct) {
        var campaign = _campaigns.Find(req.Id, DateTime.UtcNow);
        if (campaign == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        await SendAsync(_campaigns.ToDto(campaign), cancellation: ct);
    }
}
