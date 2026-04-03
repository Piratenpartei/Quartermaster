using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.Users;

public class OidcLoginStartEndpoint : Endpoint<EmptyRequest> {
    private readonly OptionRepository _optionRepo;

    public OidcLoginStartEndpoint(OptionRepository optionRepo) {
        _optionRepo = optionRepo;
    }

    public override void Configure() {
        Get("/api/users/OidcLoginStart");
        AllowAnonymous();
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct) {
        var authority = _optionRepo.GetGlobalValue("auth.oidc.authority")?.Value;
        var clientId = _optionRepo.GetGlobalValue("auth.oidc.client_id")?.Value;

        if (string.IsNullOrEmpty(authority) || string.IsNullOrEmpty(clientId)) {
            await SendAsync(new { error = "OIDC ist nicht konfiguriert." }, 503, ct);
            return;
        }

        var codeVerifier = GenerateCodeVerifier();
        var codeChallenge = ComputeCodeChallenge(codeVerifier);

        HttpContext.Response.Cookies.Append("oidc_cv", codeVerifier, new CookieOptions {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromMinutes(10),
            Path = "/api/users/OidcCallback"
        });

        var redirectUri = $"{BaseURL}api/users/OidcCallback";
        var authorizeUrl = $"{authority.TrimEnd('/')}/protocol/openid-connect/auth"
            + $"?client_id={Uri.EscapeDataString(clientId)}"
            + $"&response_type=code"
            + $"&scope=openid%20email"
            + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
            + $"&code_challenge={codeChallenge}"
            + $"&code_challenge_method=S256";

        await SendRedirectAsync(authorizeUrl, allowRemoteRedirects: true);
    }

    private static string GenerateCodeVerifier() {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string ComputeCodeChallenge(string codeVerifier) {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Convert.ToBase64String(hash)
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
