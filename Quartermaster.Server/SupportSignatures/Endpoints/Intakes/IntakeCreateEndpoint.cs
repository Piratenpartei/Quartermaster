using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.SupportSignatures;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.SupportSignatures.Endpoints.Intakes;

public class IntakeCreateEndpoint : Endpoint<SignatureIntakeSaveRequest, SignatureIntakeDTO> {
    private readonly SignatureCampaignRepository _campaigns;
    private readonly SignatureIntakeService _service;
    private readonly AdministrativeDivisionRepository _divisions;
    private readonly SignatureCampaignAccess _access;
    private readonly PermissionContext _perms;

    public IntakeCreateEndpoint(SignatureCampaignRepository campaigns, SignatureIntakeService service, AdministrativeDivisionRepository divisions,
        SignatureCampaignAccess access, PermissionContext perms) {
        _campaigns = campaigns;
        _service = service;
        _divisions = divisions;
        _access = access;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/support-signatures/campaigns/{Id}/intakes");
    }

    public override async Task HandleAsync(SignatureIntakeSaveRequest req, CancellationToken ct) {
        var campaign = _campaigns.Get(req.Id);
        if (campaign == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!_access.CanManage(campaign)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var (intake, outcome) = _service.Create(campaign.Id, req, _perms.UserId!.Value);
        if (!outcome.Succeeded) {
            AddError(outcome.ErrorKey!);
            await SendErrorsAsync(outcome.StatusCode, ct);
            return;
        }

        await SendAsync(intake!.ToDto(_divisions.Get(intake.AdministrativeDivisionId)?.Name ?? ""), cancellation: ct);
    }
}
