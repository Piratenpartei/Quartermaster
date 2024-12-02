using FastEndpoints;
using Saml;
using System.Threading;
using System.Threading.Tasks;

namespace Quartermaster.Server.Users;

//http://localhost:5232/api/users/SamlLoginStart
public class SamlLoginStartEndpoint : Endpoint<EmptyRequest> {
    public override void Configure() {
        Get("/api/users/SamlLoginStart");
        AllowAnonymous();
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct) {
        var request = new AuthRequest(Config["SamlSettings:SamlClientId"],
            $"{BaseURL}api/users/SamlConsume");

        var url = request.GetRedirectUrl(Config["SamlSettings:SamlEndpoint"]);
        await SendRedirectAsync(url, allowRemoteRedirects: true);
    }
}