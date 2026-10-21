using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures.Endpoints.Intakes;

public class IntakeUpdateEndpoint : Endpoint<SignatureIntakeSaveRequest, SignatureIntakeDTO> {
    private readonly SignatureIntakeRepository _intakes;
    private readonly SignatureCampaignRepository _campaigns;
    private readonly SignatureIntakeService _service;
    private readonly AdministrativeDivisionRepository _divisions;
    private readonly SignatureCampaignAccess _access;

    public IntakeUpdateEndpoint(SignatureIntakeRepository intakes, SignatureCampaignRepository campaigns, SignatureIntakeService service,
        AdministrativeDivisionRepository divisions, SignatureCampaignAccess access) {
        _intakes = intakes;
        _campaigns = campaigns;
        _service = service;
        _divisions = divisions;
        _access = access;
    }

    public override void Configure() {
        Put("/api/support-signatures/intakes/{Id}");
    }

    public override async Task HandleAsync(SignatureIntakeSaveRequest req, CancellationToken ct) {
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

        var outcome = _service.Update(intake, req);
        if (!outcome.Succeeded) {
            AddError(outcome.ErrorKey!);
            await SendErrorsAsync(outcome.StatusCode, ct);
            return;
        }

        var updated = _intakes.Get(intake.Id)!;
        await SendAsync(updated.ToDto(_divisions.Get(updated.AdministrativeDivisionId)?.Name ?? ""), cancellation: ct);
    }
}
