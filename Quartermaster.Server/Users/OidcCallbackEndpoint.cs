using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Quartermaster.Server.Authentication;
using Quartermaster.Data.ChapterAssociates;
using Quartermaster.Data.Members;
using Quartermaster.Data.Options;
using Quartermaster.Data.Tokens;
using Quartermaster.Data.Users;
using Quartermaster.Server.Members;

namespace Quartermaster.Server.Users;

public class OidcCallbackEndpoint : Endpoint<OidcCallbackRequest> {
    private static readonly TimeSpan DiscoveryCacheTtl = TimeSpan.FromHours(1);

    private readonly OptionRepository _optionRepo;
    private readonly UserRepository _userRepo;
    private readonly MemberRepository _memberRepo;
    private readonly TokenRepository _tokenRepo;
    private readonly ChapterOfficerRepository _officerRepo;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly KeycloakDepartmentChapterAssigner _departments;

    public OidcCallbackEndpoint(
        OptionRepository optionRepo,
        UserRepository userRepo,
        MemberRepository memberRepo,
        TokenRepository tokenRepo,
        ChapterOfficerRepository officerRepo,
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        KeycloakDepartmentChapterAssigner departments) {
        _optionRepo = optionRepo;
        _userRepo = userRepo;
        _memberRepo = memberRepo;
        _tokenRepo = tokenRepo;
        _officerRepo = officerRepo;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _departments = departments;
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
        var expectedState = HttpContext.Request.Cookies["oidc_state"];
        var expectedNonce = HttpContext.Request.Cookies["oidc_nonce"];
        if (string.IsNullOrEmpty(codeVerifier) || string.IsNullOrEmpty(expectedState) || string.IsNullOrEmpty(expectedNonce)) {
            await SendRedirectAsync("/Login?error=oidc_expired", allowRemoteRedirects: false);
            return;
        }
        ClearOidcCookies();

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(req.State ?? ""),
                Encoding.UTF8.GetBytes(expectedState))) {
            Logger.LogWarning("OIDC login failed: state mismatch");
            await SendRedirectAsync("/Login?error=oidc_state", allowRemoteRedirects: false);
            return;
        }

        var tokenEndpoint = $"{authority.TrimEnd('/')}/protocol/openid-connect/token";
        var redirectUri = $"{BaseURL}api/users/OidcCallback";

        var httpClient = _httpClientFactory.CreateClient();
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

        string responseBody;
        try {
            responseBody = await tokenResponse.Content.ReadAsStringAsync(ct);
        } catch (Exception ex) {
            Logger.LogError(ex, "OIDC token response could not be read");
            await SendRedirectAsync("/Login?error=oidc_no_id_token", allowRemoteRedirects: false);
            return;
        }

        string? idToken;
        string? accessToken;
        try {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException($"expected JSON object, got {root.ValueKind}");
            if (!root.TryGetProperty("id_token", out var idTokenEl) || idTokenEl.ValueKind != JsonValueKind.String)
                throw new InvalidOperationException("missing or non-string 'id_token'");
            idToken = idTokenEl.GetString();
            if (string.IsNullOrEmpty(idToken))
                throw new InvalidOperationException("'id_token' is empty");
            accessToken = root.TryGetProperty("access_token", out var atEl) && atEl.ValueKind == JsonValueKind.String
                ? atEl.GetString()
                : null;
        } catch (Exception ex) {
            Logger.LogError(ex, "OIDC token response could not be parsed (shape: {Shape})", DescribePayloadShape(responseBody));
            await SendRedirectAsync("/Login?error=oidc_no_id_token", allowRemoteRedirects: false);
            return;
        }

        ClaimsPrincipal principal;
        string email;
        try {
            principal = await ValidateIdToken(idToken, accessToken, expectedNonce, authority, clientId, ct);
            email = ExtractEmail(principal);
        } catch (Exception ex) {
            Logger.LogError(ex, "OIDC JWT validation failed");
            await SendRedirectAsync("/Login?error=oidc_invalid_token", allowRemoteRedirects: false);
            return;
        }

        var memberNumberClaim = _optionRepo.GetGlobalValue("auth.oidc.member_number_claim")?.Value;
        if (string.IsNullOrEmpty(memberNumberClaim))
            memberNumberClaim = "member_number";

        var rawMemberNumber = principal.FindFirst(memberNumberClaim)?.Value;
        if (!MemberNumberParser.TryParse(rawMemberNumber, out var memberNumber)) {
            Logger.LogWarning("OIDC login failed: claim {Claim} missing or not a member number. Value={Value}",
                memberNumberClaim, rawMemberNumber);
            await SendRedirectAsync("/Login?error=sso_no_member_number", allowRemoteRedirects: false);
            return;
        }

        var departmentClaim = _optionRepo.GetGlobalValue("auth.oidc.department_claim")?.Value;
        if (string.IsNullOrEmpty(departmentClaim))
            departmentClaim = "ekklesia_department";

        var identity = new SsoIdentity(email, memberNumber,
            FindClaim(principal, "given_name", ClaimTypes.GivenName),
            FindClaim(principal, "family_name", ClaimTypes.Surname),
            FindClaim(principal, departmentClaim, departmentClaim));

        Logger.LogInformation("OIDC login attempt for member number {MemberNumber}", memberNumber);

        var member = SsoLoginHelper.ResolveOrCreateMember(identity, _memberRepo);
        var issuedIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = HttpContext.Request.Headers.UserAgent.ToString();
        var (result, token) = SsoLoginHelper.ProcessSsoLogin(member, email,
            issuedIp,
            string.IsNullOrEmpty(userAgent) ? null : userAgent,
            _memberRepo, _userRepo, _tokenRepo, _officerRepo);

        switch (result) {
            case SsoLoginResult.MemberExited:
            case SsoLoginResult.UserDeleted:
                await SendRedirectAsync("/Login?error=saml_member_exited", allowRemoteRedirects: false);
                return;
        }

        _departments.Assign(member, identity.Department);
        AuthCookie.Set(HttpContext, token!.Content, token.Expires);
        await SendRedirectAsync("/", allowRemoteRedirects: false);
    }

    private void ClearOidcCookies() {
        var opts = new Microsoft.AspNetCore.Http.CookieOptions { Path = "/api/users/OidcCallback" };
        HttpContext.Response.Cookies.Delete("oidc_cv", opts);
        HttpContext.Response.Cookies.Delete("oidc_state", opts);
        HttpContext.Response.Cookies.Delete("oidc_nonce", opts);
    }

    private async Task<ClaimsPrincipal> ValidateIdToken(
        string idToken, string? accessToken, string expectedNonce,
        string authority, string expectedAudience, CancellationToken ct) {

        var discovery = await GetCachedDiscovery(authority, ct);
        var jwksUri = discovery.GetProperty("jwks_uri").GetString()!;
        var issuer = discovery.GetProperty("issuer").GetString();

        var httpClient = _httpClientFactory.CreateClient();
        var jwksJson = await httpClient.GetStringAsync(jwksUri, ct);
        var jwks = new JsonWebKeySet(jwksJson);

        var handler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = expectedAudience,
            ValidateLifetime = true,
            IssuerSigningKeys = jwks.GetSigningKeys(),
            ValidateIssuerSigningKey = true
        };

        var principal = handler.ValidateToken(idToken, validationParameters, out var validated);
        var jwt = (JwtSecurityToken)validated;

        // Nonce binds the ID token to this specific authorize round-trip. Required by OIDC spec
        // when nonce was sent in the request (we always send one).
        var nonceClaim = jwt.Claims.FirstOrDefault(c => c.Type == "nonce")?.Value;
        if (string.IsNullOrEmpty(nonceClaim) || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(nonceClaim),
                Encoding.UTF8.GetBytes(expectedNonce))) {
            throw new SecurityTokenValidationException("ID token nonce mismatch");
        }

        // at_hash binds the access token to the ID token. Per OIDC core §3.1.3.6, MUST be
        // verified when an access token is present in a code-flow response.
        if (!string.IsNullOrEmpty(accessToken)) {
            var atHashClaim = jwt.Claims.FirstOrDefault(c => c.Type == "at_hash")?.Value;
            if (string.IsNullOrEmpty(atHashClaim) || atHashClaim != ComputeAtHash(accessToken))
                throw new SecurityTokenValidationException("ID token at_hash mismatch");
        }

        return principal;
    }

    private static string ExtractEmail(ClaimsPrincipal principal) {
        var email = principal.FindFirst("email")?.Value
            ?? principal.FindFirst(ClaimTypes.Email)?.Value;

        if (string.IsNullOrEmpty(email))
            throw new InvalidOperationException("ID token does not contain an email claim");

        return email;
    }

    /// <summary>JwtSecurityTokenHandler maps standard claim names to <see cref="ClaimTypes"/> URIs by default, so both spellings are checked.</summary>
    private static string? FindClaim(ClaimsPrincipal principal, string jwtName, string mappedName) {
        var value = principal.FindFirst(jwtName)?.Value ?? principal.FindFirst(mappedName)?.Value;
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return value.Trim();
    }

    private async Task<JsonElement> GetCachedDiscovery(string authority, CancellationToken ct) {
        var key = $"oidc_discovery::{authority}";
        if (_cache.TryGetValue<JsonElement>(key, out var cached))
            return cached;

        var url = $"{authority.TrimEnd('/')}/.well-known/openid-configuration";
        var httpClient = _httpClientFactory.CreateClient();
        var discovery = await httpClient.GetFromJsonAsync<JsonElement>(url, ct);
        _cache.Set(key, discovery, DiscoveryCacheTtl);
        return discovery;
    }

    /// <summary>Returns a short description of the response body for diagnostic logging without leaking secrets (token values stay out of logs; only top-level key names go in).</summary>
    private static string DescribePayloadShape(string body) {
        try {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return $"JSON {doc.RootElement.ValueKind}";
            return $"keys=[{string.Join(",", doc.RootElement.EnumerateObject().Select(p => p.Name))}]";
        } catch (JsonException) {
            return $"non-JSON ({body.Length} chars)";
        }
    }

    private static string ComputeAtHash(string accessToken) {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(accessToken));
        var half = hash.AsSpan(0, hash.Length / 2);
        return Convert.ToBase64String(half).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}

public class OidcCallbackRequest {
    [QueryParam]
    public string? Code { get; set; }

    [QueryParam]
    public string? State { get; set; }

    [QueryParam]
    public string? Error { get; set; }

    [QueryParam, BindFrom("error_description")]
    public string? ErrorDescription { get; set; }
}
