using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.SupportSignatures.Endpoints.Members;

public class MemberCampaignGetEndpoint : Endpoint<SignatureCampaignIdRequest, MemberSignatureCampaignDTO> {
    private readonly MemberSignatureCampaigns _campaigns;
    private readonly PermissionContext _perms;
    private readonly SignatureCampaignAccess _access;

    public MemberCampaignGetEndpoint(MemberSignatureCampaigns campaigns, PermissionContext perms, SignatureCampaignAccess access) {
        _campaigns = campaigns;
        _perms = perms;
        _access = access;
    }

    public override void Configure() {
        Get("/api/support-signatures/member-campaigns/{Id}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(SignatureCampaignIdRequest req, CancellationToken ct) {
        var campaign = _campaigns.Find(req.Id, DateTime.UtcNow, _perms.UserId != null);
        if (campaign == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var dto = _campaigns.ToDto(campaign);
        dto.CanManage = _access.CanManage(campaign);
        await SendAsync(dto, cancellation: ct);
    }
}
