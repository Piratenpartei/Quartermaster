using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures.Endpoints.Intakes;

public class IntakeDeleteEndpoint : Endpoint<SignatureIntakeIdRequest> {
    private readonly SignatureIntakeRepository _intakes;
    private readonly SignatureCampaignRepository _campaigns;
    private readonly SignatureIntakeService _service;
    private readonly SignatureCampaignAccess _access;

    public IntakeDeleteEndpoint(SignatureIntakeRepository intakes, SignatureCampaignRepository campaigns, SignatureIntakeService service,
        SignatureCampaignAccess access) {
        _intakes = intakes;
        _campaigns = campaigns;
        _service = service;
        _access = access;
    }

    public override void Configure() {
        Delete("/api/support-signatures/intakes/{Id}");
    }

    public override async Task HandleAsync(SignatureIntakeIdRequest req, CancellationToken ct) {
        var intake = _intakes.Get(req.Id);
        var campaign = intake == null ? null : _campaigns.Get(intake.CampaignId);
        if (intake == null || campaign == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!_access.CanManage(campaign)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var outcome = _service.Delete(intake);
        if (!outcome.Succeeded) {
            AddError(outcome.ErrorKey!);
            await SendErrorsAsync(outcome.StatusCode, ct);
            return;
        }

        await SendNoContentAsync(ct);
    }
}
