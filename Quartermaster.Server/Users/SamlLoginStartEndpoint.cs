using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Data.Options;
using Saml;

namespace Quartermaster.Server.Users;

public class SamlLoginStartEndpoint : Endpoint<EmptyRequest> {
    private readonly OptionRepository _optionRepo;

    public SamlLoginStartEndpoint(OptionRepository optionRepo) {
        _optionRepo = optionRepo;
    }

    public override void Configure() {
        Get("/api/users/SamlLoginStart");
        AllowAnonymous();
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct) {
        var clientId = _optionRepo.GetGlobalValue("auth.saml.client_id")?.Value;
        var endpoint = _optionRepo.GetGlobalValue("auth.saml.endpoint")?.Value;

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(endpoint)) {
            await SendAsync(new { error = "SAML ist nicht konfiguriert." }, 503, ct);
            return;
        }

        var request = new AuthRequest(clientId, $"{BaseURL}api/users/SamlConsume");
        var url = request.GetRedirectUrl(endpoint);
        await SendRedirectAsync(url, allowRemoteRedirects: true);
    }
}
