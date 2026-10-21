using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures.Endpoints.Members;

public class MemberCampaignFormEndpoint : Endpoint<SignatureCampaignIdRequest> {
    private readonly MemberSignatureCampaigns _campaigns;
    private readonly SupportSignatureFormStorage _storage;

    public MemberCampaignFormEndpoint(MemberSignatureCampaigns campaigns, SupportSignatureFormStorage storage) {
        _campaigns = campaigns;
        _storage = storage;
    }

    public override void Configure() {
        Get("/api/support-signatures/member-campaigns/{Id}/form");
    }

    public override async Task HandleAsync(SignatureCampaignIdRequest req, CancellationToken ct) {
        var campaign = _campaigns.Find(req.Id, DateTime.UtcNow);
        var stream = campaign == null || !campaign.HasForm ? null : _storage.OpenRead(campaign.Id);
        if (stream == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        await SendStreamAsync(stream, campaign!.FormFileName ?? "Formular.pdf", stream.Length, "application/pdf", cancellation: ct);
    }
}
