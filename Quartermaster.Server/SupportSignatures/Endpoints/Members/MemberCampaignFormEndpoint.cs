using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.SupportSignatures.Endpoints.Members;

public class MemberCampaignFormEndpoint : Endpoint<SignatureCampaignIdRequest> {
    private readonly MemberSignatureCampaigns _campaigns;
    private readonly PermissionContext _perms;
    private readonly SupportSignatureFormStorage _storage;

    public MemberCampaignFormEndpoint(MemberSignatureCampaigns campaigns, PermissionContext perms, SupportSignatureFormStorage storage) {
        _campaigns = campaigns;
        _perms = perms;
        _storage = storage;
    }

    public override void Configure() {
        Get("/api/support-signatures/member-campaigns/{Id}/form");
        AllowAnonymous();
    }

    public override async Task HandleAsync(SignatureCampaignIdRequest req, CancellationToken ct) {
        var campaign = _campaigns.Find(req.Id, DateTime.UtcNow, _perms.UserId != null);
        var stream = campaign == null || !campaign.HasForm ? null : _storage.OpenRead(campaign.Id);
        if (stream == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        await SendStreamAsync(stream, campaign!.FormFileName ?? "Formular.pdf", stream.Length, "application/pdf", cancellation: ct);
    }
}
