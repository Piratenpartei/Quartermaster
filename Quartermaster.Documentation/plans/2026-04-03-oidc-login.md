# OpenID Connect Login Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add OpenID Connect as an alternative SSO login method alongside the existing SAML flow, using manual authorization code exchange (no ASP.NET Core OIDC middleware).

**Architecture:** Two new FastEndpoints handle the OIDC flow: one redirects the user to the IdP's authorize endpoint with a PKCE challenge, and another receives the callback with an authorization code, exchanges it for an ID token via HTTP, validates the JWT, extracts the email claim, then feeds into the existing member→user→token pipeline (shared with SAML). The Blazor callback page is reused. Configuration is stored in the Options system alongside existing SAML options.

**Tech Stack:** FastEndpoints, System.Net.Http (token exchange), System.IdentityModel.Tokens.Jwt (JWT validation), PKCE (SHA256 code challenge), existing Options/Member/User/Token infrastructure.

---

### Task 1: Add System.IdentityModel.Tokens.Jwt NuGet package

**Files:**
- Modify: `Quartermaster.Server/Quartermaster.Server.csproj`

- [ ] **Step 1: Add the package**

```bash
cd /media/SMB/Quartermaster && DOTNET_ROOT=/usr/lib/dotnet /usr/lib/dotnet/dotnet add Quartermaster.Server package System.IdentityModel.Tokens.Jwt
```

- [ ] **Step 2: Build to verify**

```bash
cd /media/SMB/Quartermaster && DOTNET_ROOT=/usr/lib/dotnet /usr/lib/dotnet/dotnet build
```

Expected: 0 errors.

---

### Task 2: Seed OIDC options in OptionRepository

**Files:**
- Modify: `Quartermaster.Data/Options/OptionRepository.cs` (SupplementDefaults method, after auth.sso.support_contact block ~line 137)

- [ ] **Step 1: Add OIDC option definitions**

Add these after the `auth.sso.support_contact` block:

```csharp
AddDefinitionIfNotExists("auth.oidc.authority",
    "OIDC: Authority-URL (z.B. https://keycloak/realms/master)",
    OptionDataType.String, false, "", "");

AddDefinitionIfNotExists("auth.oidc.client_id",
    "OIDC: Client-ID",
    OptionDataType.String, false, "", "");

AddDefinitionIfNotExists("auth.oidc.client_secret",
    "OIDC: Client-Secret",
    OptionDataType.String, false, "", "");

AddDefinitionIfNotExists("auth.oidc.button_text",
    "OIDC: Login-Button Text",
    OptionDataType.String, false, "", "OpenID Login");
```

- [ ] **Step 2: Build to verify**

```bash
cd /media/SMB/Quartermaster && DOTNET_ROOT=/usr/lib/dotnet /usr/lib/dotnet/dotnet build
```

Expected: 0 errors.

---

### Task 3: Add OidcEnabled/OidcButtonText to ClientConfigDTO and pipeline

**Files:**
- Modify: `Quartermaster.Api/Config/ClientConfigDTO.cs`
- Modify: `Quartermaster.Server/Config/ClientConfigEndpoint.cs`
- Modify: `Quartermaster.Blazor/Services/ClientConfigService.cs`

- [ ] **Step 1: Add properties to ClientConfigDTO**

Add after the `SsoSupportContact` property in `Quartermaster.Api/Config/ClientConfigDTO.cs`:

```csharp
public bool OidcEnabled { get; set; }
public string OidcButtonText { get; set; } = "";
```

- [ ] **Step 2: Populate in ClientConfigEndpoint**

In `Quartermaster.Server/Config/ClientConfigEndpoint.cs`, add after the `ssoSupportContact` variable:

```csharp
var oidcAuthority = _optionRepo.GetGlobalValue("auth.oidc.authority")?.Value ?? "";
var oidcButtonText = _optionRepo.GetGlobalValue("auth.oidc.button_text")?.Value ?? "OpenID Login";
```

And add to the `SendAsync` DTO:

```csharp
OidcEnabled = !string.IsNullOrEmpty(oidcAuthority),
OidcButtonText = oidcButtonText
```

- [ ] **Step 3: Expose in ClientConfigService**

Add after `SsoSupportContact` in `Quartermaster.Blazor/Services/ClientConfigService.cs`:

```csharp
public bool OidcEnabled => _config?.OidcEnabled ?? false;
public string OidcButtonText => _config?.OidcButtonText ?? "OpenID Login";
```

- [ ] **Step 4: Build to verify**

```bash
cd /media/SMB/Quartermaster && DOTNET_ROOT=/usr/lib/dotnet /usr/lib/dotnet/dotnet build
```

Expected: 0 errors.

---

### Task 4: Extract shared SsoLoginHelper from SamlLoginConsumeEndpoint

The member→user→token pipeline is identical for SAML and OIDC. Extract it into a static helper so both endpoints call the same code.

**Files:**
- Create: `Quartermaster.Server/Users/SsoLoginHelper.cs`
- Modify: `Quartermaster.Server/Users/SamlLoginConsumeEndpoint.cs`

- [ ] **Step 1: Create SsoLoginHelper**

Create `Quartermaster.Server/Users/SsoLoginHelper.cs`:

```csharp
using System;
using Quartermaster.Data.Members;
using Quartermaster.Data.Tokens;
using Quartermaster.Data.Users;

namespace Quartermaster.Server.Users;

public enum SsoLoginResult {
    Success,
    NoMember,
    MemberExited,
    UserDeleted
}

public static class SsoLoginHelper {
    public static (SsoLoginResult Result, string? TokenContent) ProcessSsoLogin(
        string email,
        MemberRepository memberRepo,
        UserRepository userRepo,
        TokenRepository tokenRepo) {

        var member = memberRepo.GetByEmail(email);
        if (member == null)
            return (SsoLoginResult.NoMember, null);

        if (member.ExitDate.HasValue)
            return (SsoLoginResult.MemberExited, null);

        var user = member.UserId.HasValue ? userRepo.GetById(member.UserId.Value) : null;

        if (user == null) {
            user = userRepo.GetByEmail(email);

            if (user == null) {
                user = new User {
                    EMail = email,
                    Username = email,
                    FirstName = member.FirstName ?? "",
                    LastName = member.LastName ?? ""
                };
                userRepo.Create(user);
            }

            memberRepo.SetUserId(member.Id, user.Id);
        }

        if (user.DeletedAt.HasValue)
            return (SsoLoginResult.UserDeleted, null);

        var token = tokenRepo.LoginUser(user.Id);
        return (SsoLoginResult.Success, token.Content);
    }
}
```

- [ ] **Step 2: Refactor SamlLoginConsumeEndpoint to use SsoLoginHelper**

Replace lines 88–131 of `SamlLoginConsumeEndpoint.cs` (from `// Find member by email` to `SendRedirectAsync($"/Login/SamlCallback#...") `) with:

```csharp
        var (result, tokenContent) = SsoLoginHelper.ProcessSsoLogin(email, _memberRepo, _userRepo, _tokenRepo);

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
```

- [ ] **Step 3: Build and test**

```bash
cd /media/SMB/Quartermaster && DOTNET_ROOT=/usr/lib/dotnet /usr/lib/dotnet/dotnet build && DOTNET_ROOT=/usr/lib/dotnet /usr/lib/dotnet/dotnet test
```

Expected: 0 errors, all tests pass. SAML behavior is unchanged.

---

### Task 5: Create OidcLoginStartEndpoint

This endpoint redirects the user to the IdP's authorization endpoint with PKCE parameters.

**Files:**
- Create: `Quartermaster.Server/Users/OidcLoginStartEndpoint.cs`

- [ ] **Step 1: Create the endpoint**

Create `Quartermaster.Server/Users/OidcLoginStartEndpoint.cs`:

```csharp
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

        // Generate PKCE code verifier and challenge
        var codeVerifier = GenerateCodeVerifier();
        var codeChallenge = ComputeCodeChallenge(codeVerifier);

        // Store code verifier in a short-lived cookie for the callback
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
```

- [ ] **Step 2: Build to verify**

```bash
cd /media/SMB/Quartermaster && DOTNET_ROOT=/usr/lib/dotnet /usr/lib/dotnet/dotnet build
```

Expected: 0 errors.

---

### Task 6: Create OidcCallbackEndpoint

This endpoint receives the authorization code, exchanges it for an ID token, validates the JWT, extracts the email, and uses `SsoLoginHelper`.

**Files:**
- Create: `Quartermaster.Server/Users/OidcCallbackEndpoint.cs`

- [ ] **Step 1: Create the endpoint**

Create `Quartermaster.Server/Users/OidcCallbackEndpoint.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
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

    public OidcCallbackEndpoint(
        OptionRepository optionRepo,
        UserRepository userRepo,
        MemberRepository memberRepo,
        TokenRepository tokenRepo) {
        _optionRepo = optionRepo;
        _userRepo = userRepo;
        _memberRepo = memberRepo;
        _tokenRepo = tokenRepo;
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

        // Retrieve PKCE code verifier from cookie
        var codeVerifier = HttpContext.Request.Cookies["oidc_cv"];
        if (string.IsNullOrEmpty(codeVerifier)) {
            await SendRedirectAsync("/Login?error=oidc_expired", allowRemoteRedirects: false);
            return;
        }

        // Clear the cookie
        HttpContext.Response.Cookies.Delete("oidc_cv", new Microsoft.AspNetCore.Http.CookieOptions {
            Path = "/api/users/OidcCallback"
        });

        // Exchange authorization code for tokens
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

        // Validate JWT and extract email
        string email;
        try {
            email = await ValidateAndExtractEmail(idToken, authority, clientId, ct);
        } catch (Exception ex) {
            Logger.LogError(ex, "OIDC JWT validation failed");
            await SendRedirectAsync("/Login?error=oidc_invalid_token", allowRemoteRedirects: false);
            return;
        }

        Logger.LogInformation("OIDC login attempt for email: {Email}", email);

        var (result, tokenContent) = SsoLoginHelper.ProcessSsoLogin(email, _memberRepo, _userRepo, _tokenRepo);

        switch (result) {
            case SsoLoginResult.NoMember:
                await SendRedirectAsync("/Login?error=saml_no_member", allowRemoteRedirects: false);
                return;
            case SsoLoginResult.MemberExited:
            case SsoLoginResult.UserDeleted:
                await SendRedirectAsync("/Login?error=saml_member_exited", allowRemoteRedirects: false);
                return;
        }

        // Reuse the same Blazor callback page as SAML
        await SendRedirectAsync($"/Login/SamlCallback#{tokenContent}", allowRemoteRedirects: false);
    }

    private static async Task<string> ValidateAndExtractEmail(
        string idToken, string authority, string expectedAudience, CancellationToken ct) {

        // Fetch JWKS from the IdP's discovery endpoint
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
```

- [ ] **Step 2: Build to verify**

```bash
cd /media/SMB/Quartermaster && DOTNET_ROOT=/usr/lib/dotnet /usr/lib/dotnet/dotnet build
```

Expected: 0 errors.

---

### Task 7: Update Login page to show OIDC option

**Files:**
- Modify: `Quartermaster.Blazor/Pages/Login.razor`
- Modify: `Quartermaster.Blazor/Pages/Login.razor.cs`

- [ ] **Step 1: Update Login.razor to show OIDC card**

Replace the current two-column layout (the `<div class="row g-4"...>` block) with a three-column layout that shows SSO options dynamically. The SSO card (SAML or OIDC, whichever is enabled) shows on the left, manual login on the right. If both SSO methods are enabled, show both. Replace the full `<div class="row g-4"...>` block:

```razor
<div class="d-flex justify-content-center mt-5">
    <div class="row g-4" style="max-width: 700px; width: 100%;">
        @if (ConfigService.SamlEnabled) {
            <div class="col-12 col-md-6">
                <a href="/api/users/SamlLoginStart" class="card text-decoration-none h-100 btn-hover-scale1">
                    <div class="card-body text-center d-flex flex-column justify-content-center" style="min-height: 160px;">
                        <i class="bi bi-shield-lock fs-1 mb-2"></i>
                        <h5 class="card-title">@ConfigService.SamlButtonText</h5>
                        <p class="card-text text-body-secondary">Anmeldung via SSO</p>
                    </div>
                </a>
            </div>
        }
        @if (ConfigService.OidcEnabled) {
            <div class="col-12 col-md-6">
                <a href="/api/users/OidcLoginStart" class="card text-decoration-none h-100 btn-hover-scale1">
                    <div class="card-body text-center d-flex flex-column justify-content-center" style="min-height: 160px;">
                        <i class="bi bi-key fs-1 mb-2"></i>
                        <h5 class="card-title">@ConfigService.OidcButtonText</h5>
                        <p class="card-text text-body-secondary">Anmeldung via OpenID</p>
                    </div>
                </a>
            </div>
        }
        @if (!ConfigService.SamlEnabled && !ConfigService.OidcEnabled) {
            <div class="col-12 col-md-6">
                <div class="card h-100 text-body-tertiary" title="SSO ist nicht konfiguriert">
                    <div class="card-body text-center d-flex flex-column justify-content-center" style="min-height: 160px;">
                        <i class="bi bi-shield-lock fs-1 mb-2"></i>
                        <h5 class="card-title">SSO Login</h5>
                        <p class="card-text">Nicht verfügbar</p>
                    </div>
                </div>
            </div>
        }
        <div class="col-12 col-md-6">
            <a href="/Login/Manual" class="card text-decoration-none h-100 btn-hover-scale1">
                <div class="card-body text-center d-flex flex-column justify-content-center" style="min-height: 160px;">
                    <i class="bi bi-person-fill fs-1 mb-2"></i>
                    <h5 class="card-title">Manuell anmelden</h5>
                    <p class="card-text text-body-secondary">Mit Benutzername und Passwort</p>
                </div>
            </a>
        </div>
    </div>
</div>
```

- [ ] **Step 2: Add OIDC error messages to Login.razor.cs**

Add these cases to the error switch in `Login.razor.cs`:

```csharp
"oidc_idp_error" => "Die OpenID-Anmeldung wurde vom Identitätsanbieter abgelehnt.",
"oidc_no_code" => "Die OpenID-Anmeldung ist fehlgeschlagen (kein Autorisierungscode erhalten).",
"oidc_not_configured" => "OpenID Connect ist nicht konfiguriert.",
"oidc_expired" => "Die OpenID-Anmeldung ist abgelaufen. Bitte erneut versuchen.",
"oidc_exchange_failed" => "Die OpenID-Anmeldung ist fehlgeschlagen (Token-Austausch fehlgeschlagen).",
"oidc_no_id_token" => "Die OpenID-Anmeldung ist fehlgeschlagen (kein ID-Token erhalten).",
"oidc_invalid_token" => "Die OpenID-Anmeldung ist fehlgeschlagen (ungültiges Token).",
```

- [ ] **Step 3: Build to verify**

```bash
cd /media/SMB/Quartermaster && DOTNET_ROOT=/usr/lib/dotnet /usr/lib/dotnet/dotnet build
```

Expected: 0 errors.

---

### Task 8: Run tests, restart server, verify

- [ ] **Step 1: Run all tests**

```bash
cd /media/SMB/Quartermaster && DOTNET_ROOT=/usr/lib/dotnet /usr/lib/dotnet/dotnet test
```

Expected: All tests pass.

- [ ] **Step 2: Kill old server, restart**

```bash
lsof -ti :7213 -ti :5232 2>/dev/null | sort -u | xargs kill -9 2>/dev/null
sleep 3
cd /media/SMB/Quartermaster/Quartermaster.Server && DOTNET_ROOT=/usr/lib/dotnet /usr/lib/dotnet/dotnet run
```

- [ ] **Step 3: Verify page loads in Chrome**

Navigate to `https://192.168.42.103:7213/Login` and verify:
- If SAML is configured: SAML card shows
- If OIDC is not yet configured: no OIDC card shows
- Manual login card always shows
- No console errors

- [ ] **Step 4: Configure OIDC options in admin UI**

Set these options:
- `auth.oidc.authority` → `http://192.168.42.87:8080/realms/master`
- `auth.oidc.client_id` → the OIDC client ID from Keycloak
- `auth.oidc.client_secret` → the client secret from Keycloak

- [ ] **Step 5: Test OIDC login flow**

Hard-refresh the login page, click the OpenID card, authenticate with Keycloak, verify redirect back and successful login.
