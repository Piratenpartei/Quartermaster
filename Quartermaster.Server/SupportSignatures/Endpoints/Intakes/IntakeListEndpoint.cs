using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures.Endpoints.Intakes;

public class IntakeListEndpoint : Endpoint<SignatureCampaignIdRequest, List<SignatureIntakeDTO>> {
    private readonly SignatureCampaignRepository _campaigns;
    private readonly SignatureIntakeRepository _intakes;
    private readonly AdministrativeDivisionRepository _divisions;
    private readonly SignatureCampaignAccess _access;

    public IntakeListEndpoint(SignatureCampaignRepository campaigns, SignatureIntakeRepository intakes, AdministrativeDivisionRepository divisions,
        SignatureCampaignAccess access) {
        _campaigns = campaigns;
        _intakes = intakes;
        _divisions = divisions;
        _access = access;
    }

    public override void Configure() {
        Get("/api/support-signatures/campaigns/{Id}/intakes");
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

        var intakes = _intakes.List(campaign.Id);
        var names = _divisions.GetMany(intakes.Select(i => i.AdministrativeDivisionId))
            .ToDictionary(d => d.Id, d => d.Name);
        await SendAsync(intakes.Select(i => i.ToDto(names.GetValueOrDefault(i.AdministrativeDivisionId, ""))).ToList(), cancellation: ct);
    }
}
