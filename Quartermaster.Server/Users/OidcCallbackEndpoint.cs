using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Quartermaster.Data.ChapterAssociates;
using Quartermaster.Data.Members;
using Quartermaster.Data.Options;
using Quartermaster.Data.Tokens;
using Quartermaster.Data.Users;

namespace Quartermaster.Server.Users;

public class OidcCallbackEndpoint : Endpoint<OidcCallbackRequest> {
    private readonly OptionRepository _optionRepo;
    private readonly UserRepository _userRepo;
    private readonly MemberRepository _memberRepo;
    private readonly TokenRepository _tokenRepo;
    private readonly ChapterOfficerRepository _officerRepo;

    public OidcCallbackEndpoint(
        OptionRepository optionRepo,
        UserRepository userRepo,
        MemberRepository memberRepo,
        TokenRepository tokenRepo,
        ChapterOfficerRepository officerRepo) {
        _optionRepo = optionRepo;
        _userRepo = userRepo;
        _memberRepo = memberRepo;
        _tokenRepo = tokenRepo;
        _officerRepo = officerRepo;
    }

    public override void Configure() {
        Get("/api/users/OidcCallback");
        AllowAnonymous();
    }

    public override async Task HandleAsync(OidcCallbackRequest req, CancellationToken ct) {
        if (!string.IsNullOrEmpty(req.Error)) {
            Logger.LogWarning("OIDC error from IdP: {Error} - {Description}", req.Error, req.ErrorDescription);
            await SendRedirectAsync("/Login?error=oidc_idp_error", allowRemoteRedirects: false);
            return;
        }

        if (string.IsNullOrEmpty(req.Code)) {
            await SendRedirectAsync("/Login?error=oidc_no_code", allowRemoteRedirects: false);
            return;
        }

        var authority = _optionRepo.GetGlobalValue("auth.oidc.authority")?.Value;
        var clientId = _optionRepo.GetGlobalValue("auth.oidc.client_id")?.Value;
        var clientSecret = _optionRepo.GetGlobalValue("auth.oidc.client_secret")?.Value ?? "";

        if (string.IsNullOrEmpty(authority) || string.IsNullOrEmpty(clientId)) {
            await SendRedirectAsync("/Login?error=oidc_not_configured", allowRemoteRedirects: false);
            return;
        }

        var codeVerifier = HttpContext.Request.Cookies["oidc_cv"];
        if (string.IsNullOrEmpty(codeVerifier)) {
            await SendRedirectAsync("/Login?error=oidc_expired", allowRemoteRedirects: false);
            return;
        }

        HttpContext.Response.Cookies.Delete("oidc_cv", new Microsoft.AspNetCore.Http.CookieOptions {
            Path = "/api/users/OidcCallback"
        });

        var tokenEndpoint = $"{authority.TrimEnd('/')}/protocol/openid-connect/token";
        var redirectUri = $"{BaseURL}api/users/OidcCallback";

        using var httpClient = new HttpClient();
        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string> {
            ["grant_type"] = "authorization_code",
            ["code"] = req.Code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = clientId,
            ["code_verifier"] = codeVerifier,
            ["client_secret"] = clientSecret
        });

        HttpResponseMessage tokenResponse;
        try {
            tokenResponse = await httpClient.PostAsync(tokenEndpoint, tokenRequest, ct);
        } catch (Exception ex) {
            Logger.LogError(ex, "OIDC token exchange HTTP request failed");
            await SendRedirectAsync("/Login?error=oidc_exchange_failed", allowRemoteRedirects: false);
            return;
        }

        if (!tokenResponse.IsSuccessStatusCode) {
            var body = await tokenResponse.Content.ReadAsStringAsync(ct);
            Logger.LogWarning("OIDC token exchange failed: {Status} {Body}", tokenResponse.StatusCode, body);
            await SendRedirectAsync("/Login?error=oidc_exchange_failed", allowRemoteRedirects: false);
            return;
        }

        var tokenData = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
        var idToken = tokenData.GetProperty("id_token").GetString();

        if (string.IsNullOrEmpty(idToken)) {
            await SendRedirectAsync("/Login?error=oidc_no_id_token", allowRemoteRedirects: false);
            return;
        }

        string email;
        try {
            email = await ValidateAndExtractEmail(idToken, authority, clientId, ct);
        } catch (Exception ex) {
            Logger.LogError(ex, "OIDC JWT validation failed");
            await SendRedirectAsync("/Login?error=oidc_invalid_token", allowRemoteRedirects: false);
            return;
        }

        Logger.LogInformation("OIDC login attempt for email: {Email}", email);

        var (result, tokenContent) = SsoLoginHelper.ProcessSsoLogin(email, _memberRepo, _userRepo, _tokenRepo, _officerRepo);

        switch (result) {
            case SsoLoginResult.NoMember:
                await SendRedirectAsync("/Login?error=saml_no_member", allowRemoteRedirects: false);
                return;
            case SsoLoginResult.MemberExited:
            case SsoLoginResult.UserDeleted:
                await SendRedirectAsync("/Login?error=saml_member_exited", allowRemoteRedirects: false);
                return;
        }

        await SendRedirectAsync($"/Login/SamlCallback#{tokenContent}", allowRemoteRedirects: false);
    }

    private static async Task<string> ValidateAndExtractEmail(
        string idToken, string authority, string expectedAudience, CancellationToken ct) {

        var jwksUrl = $"{authority.TrimEnd('/')}/.well-known/openid-configuration";
        using var httpClient = new HttpClient();
        var discovery = await httpClient.GetFromJsonAsync<JsonElement>(jwksUrl, ct);
        var jwksUri = discovery.GetProperty("jwks_uri").GetString()!;

        var jwksJson = await httpClient.GetStringAsync(jwksUri, ct);
        var jwks = new JsonWebKeySet(jwksJson);

        var handler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters {
            ValidateIssuer = true,
            ValidIssuer = discovery.GetProperty("issuer").GetString(),
            ValidateAudience = true,
            ValidAudience = expectedAudience,
            ValidateLifetime = true,
            IssuerSigningKeys = jwks.GetSigningKeys(),
            ValidateIssuerSigningKey = true
        };

        var principal = handler.ValidateToken(idToken, validationParameters, out _);

        var email = principal.FindFirst("email")?.Value
            ?? principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;

        if (string.IsNullOrEmpty(email))
            throw new InvalidOperationException("ID token does not contain an email claim");

        return email;
    }
}

public class OidcCallbackRequest {
    [QueryParam]
    public string? Code { get; set; }

    [QueryParam]
    public string? Error { get; set; }

    [QueryParam, BindFrom("error_description")]
    public string? ErrorDescription { get; set; }
}
