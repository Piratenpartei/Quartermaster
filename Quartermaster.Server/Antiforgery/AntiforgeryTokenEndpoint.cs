using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.AspNetCore.Antiforgery;

namespace Quartermaster.Server.Antiforgery;

public class AntiforgeryTokenEndpoint : EndpointWithoutRequest {
    private readonly IAntiforgery _antiforgery;

    public AntiforgeryTokenEndpoint(IAntiforgery antiforgery) {
        _antiforgery = antiforgery;
    }

    public override void Configure() {
        Get("/api/antiforgery/token");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct) {
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
        await SendAsync(new { token = tokens.RequestToken }, cancellation: ct);
    }
}
