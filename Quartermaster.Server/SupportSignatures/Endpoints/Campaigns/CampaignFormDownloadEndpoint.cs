using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures.Endpoints.Campaigns;

public class CampaignFormDownloadEndpoint : Endpoint<SignatureCampaignIdRequest> {
    private readonly SignatureCampaignRepository _campaigns;
    private readonly SupportSignatureFormStorage _storage;
    private readonly SignatureCampaignAccess _access;

    public CampaignFormDownloadEndpoint(SignatureCampaignRepository campaigns, SupportSignatureFormStorage storage, SignatureCampaignAccess access) {
        _campaigns = campaigns;
        _storage = storage;
        _access = access;
    }

    public override void Configure() {
        Get("/api/support-signatures/campaigns/{Id}/form");
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

        var stream = campaign.HasForm ? _storage.OpenRead(campaign.Id) : null;
        if (stream == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        await SendStreamAsync(stream, campaign.FormFileName ?? "Formular.pdf", stream.Length, "application/pdf", cancellation: ct);
    }
}
