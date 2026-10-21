using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures.Endpoints.Offices;

public class OfficeSuggestionEndpoint : Endpoint<OfficeSuggestionRequest, OfficeSuggestionDTO> {
    private readonly CertificationOfficeSuggester _suggester;
    private readonly SignatureCampaignAccess _access;

    public OfficeSuggestionEndpoint(CertificationOfficeSuggester suggester, SignatureCampaignAccess access) {
        _suggester = suggester;
        _access = access;
    }

    public override void Configure() {
        Get("/api/support-signatures/offices/suggestion");
    }

    public override async Task HandleAsync(OfficeSuggestionRequest req, CancellationToken ct) {
        if (!_access.CanUseOffices()) {
            await SendForbiddenAsync(ct);
            return;
        }

        var suggestion = _suggester.Suggest(req.MunicipalityId);
        if (suggestion == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        await SendAsync(suggestion, cancellation: ct);
    }
}
