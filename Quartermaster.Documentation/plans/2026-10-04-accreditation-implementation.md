# Accreditation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A Quartermaster page "Vorstandsarbeit > Akkreditierung" lets a delegate set a Keycloak account's voting eligibility by member number and create temporary Keycloak accounts with a generated password.

**Architecture:** A small Keycloak admin client in `Quartermaster.Server/Accreditation/` (client-credentials token provider + four admin calls behind `IKeycloakAdminClient`), two pure generators (username rule ported from MemberDataImport, readable password), four FastEndpoints gated by a new global permission, and one Blazor page. Nothing is stored in Quartermaster except audit log rows.

**Tech Stack:** .NET 10, FastEndpoints, Blazor WASM, TUnit, System.Text.Json. No new packages.

**Spec:** `Quartermaster.Documentation/plans/2026-10-04-accreditation-design.md`

## Global Constraints

- Follow `CLAUDE.md` (K&R braces, if-body on next line, braces on multi-line conditions and any else, no `@code` in `.razor`, explicit access modifiers, `var`, fluent LINQ one call per line, `== null`, no `is not null`, no `and`/`or` patterns, no using aliases, no `global::`, one class per file except DTO files and request+endpoint pairs, comments only for a non-obvious WHY, `///` summaries describe the member contract only).
- Readonly fields `_camelCase`, mutable fields plain `camelCase`; member order consts/static, fields, properties, ctor, methods; no em or en dashes anywhere.
- Attribute names and values exactly: `member_number`, `ekklesia_verified`="true", `ekklesia_eligible`="true"|"false", `ekklesia_external_voting`="false", `created_time` in format `yyyy-MM-dd'T'HH:mm:ss.ffffff'+00:00'` (UTC).
- Username rule identical to MemberDataImport's `UsernameGenerator` (prohibited characters ``<>&"'$%!#?§,;:*~/\|^=[]{}()` `` plus control characters; dots and whitespace become the separator; hash suffix = first 8 bytes of SHA-256 over the trimmed member number, big-endian `ulong`, `% 1_000_000`, `D6`).
- Passwords are never stored, logged, or written to the audit log.
- Never create an account for a member number that already has one in any state.
- Permission `accreditation_manage` is global, in no default role, not in `SystemPermissions`.
- Commits are made by the user only; never run git. "Checkpoint" = stop and report.
- Build: `cd /media/SMB/Quartermaster && dotnet build Quartermaster.sln -m:2 -v:q -nologo`. Run one test class: `dotnet run --no-build --project Quartermaster.Server.Tests/Quartermaster.Server.Tests.csproj -- --treenode-filter "/*/*/<ClassName>/*"`. Never an unfiltered run.

## Review Focus

1. Keycloak's `q=member_number:12` search also returning `member_number` 123 or 1234: only exact (trimmed) matches may count, otherwise eligibility lands on the wrong person. Pinned in Task 1 (`HttpKeycloakAdminClientTests`).
2. A member whose account is disabled/closed (e.g. last night's temporary account, or a former member): search must show it and creation must still be refused for that number. Pinned in Task 3 (`AccreditationEndpointTests`).
3. Two delegates creating an account for the same new member number at nearly the same time: the second must not produce a duplicate silently. Keycloak does not enforce attribute uniqueness, so the endpoint re-checks after create and reports a duplicate loudly. Pinned in Task 3.
4. Names with umlauts, hyphens, double spaces, or only prohibited characters: suggestion must match MemberDataImport, and an all-prohibited name must not produce an empty username. Pinned in Task 2 (`AccreditationUsernameGeneratorTests`).
5. A password in any log line or audit row: must never happen, including on Keycloak errors that echo the request body. Pinned in Task 1 (exception message excludes request bodies) and Task 3 (audit rows checked).

---

### Task 1: Keycloak settings, token provider, admin client, options, permission

**Files:**
- Create: `Quartermaster.Server/Accreditation/KeycloakSettings.cs`, `KeycloakException.cs`, `KeycloakUserRepresentation.cs`, `KeycloakTokenProvider.cs`, `IKeycloakAdminClient.cs`, `HttpKeycloakAdminClient.cs`, `NewKeycloakUser.cs`
- Modify: `Quartermaster.Data/Options/OptionRepository.cs`, `Quartermaster.Api/PermissionIdentifier.cs`, `Quartermaster.Data/Permissions/PermissionRepository.cs`, `Quartermaster.Server/Program.cs`, `Quartermaster.Documentation/Configuration.md`
- Test: `Quartermaster.Server.Tests/Accreditation/HttpKeycloakAdminClientTests.cs` (reuses `Quartermaster.Server.Tests.NewsPosts.Fakes.StubHttpMessageHandler`)

**Interfaces:**
- Produces:
  - `KeycloakSettings.ReadFrom(OptionRepository)` with `BaseUrl`, `AuthRealm`, `Realm`, `ClientId`, `ClientSecret`, `UsernameSeparator`, `bool IsConfigured`
  - `record KeycloakUserRepresentation(string Id, string Username, string? FirstName, string? LastName, string? Email, bool Enabled, Dictionary<string, List<string>> Attributes)` with `string? Attribute(string name)` (first value, trimmed)
  - `record NewKeycloakUser(string Username, string FirstName, string LastName, string? Email, string Password, Dictionary<string, List<string>> Attributes)`
  - `IKeycloakAdminClient`: `Task<List<KeycloakUserRepresentation>> FindByMemberNumberAsync(string memberNumber, CancellationToken ct)`, `Task<bool> UsernameExistsAsync(string username, CancellationToken ct)`, `Task<string> CreateUserAsync(NewKeycloakUser user, CancellationToken ct)` (returns id), `Task SetAttributeAsync(string userId, string name, string value, CancellationToken ct)`
  - `KeycloakException(int? StatusCode, string Message)`; `KeycloakTokenProvider` (singleton) `Task<string> GetTokenAsync(HttpClient http, KeycloakSettings settings, CancellationToken ct)`
  - `PermissionIdentifier.ManageAccreditation = "accreditation_manage"`

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Net;
using System.Text.Json;
using Quartermaster.Server.Accreditation;
using Quartermaster.Server.Tests.NewsPosts.Fakes;

namespace Quartermaster.Server.Tests.Accreditation;

public class HttpKeycloakAdminClientTests {
    private const string Token = """{"access_token":"tok","expires_in":300}""";

    private static KeycloakSettings Settings() => new() {
        BaseUrl = "https://sso.test", AuthRealm = "master", Realm = "users", ClientId = "admin-api", ClientSecret = "s3cret"
    };

    private static HttpKeycloakAdminClient Client(StubHttpMessageHandler handler)
        => new(new HttpClient(handler), Settings(), new KeycloakTokenProvider());

    [Test]
    public async Task FindByMemberNumber_keeps_only_exact_matches_and_uses_bearer_token() {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, Token);
        handler.Enqueue(HttpStatusCode.OK, """
            [{"id":"u1","username":"a","enabled":true,"attributes":{"member_number":["12"]}},
             {"id":"u2","username":"b","enabled":true,"attributes":{"member_number":["123"]}},
             {"id":"u3","username":"c","enabled":false,"attributes":{"member_number":[" 12 "]}}]
            """);

        var users = await Client(handler).FindByMemberNumberAsync("12", CancellationToken.None);

        await Assert.That(users.Select(u => u.Id).ToList()).IsEquivalentTo(new List<string> { "u1", "u3" });
        await Assert.That(handler.Requests[0].RequestUri!.ToString()).IsEqualTo("https://sso.test/realms/master/protocol/openid-connect/token");
        await Assert.That(handler.Requests[1].RequestUri!.ToString()).StartsWith("https://sso.test/admin/realms/users/users?q=member_number%3A12");
        await Assert.That(handler.Requests[1].Headers.Authorization!.ToString()).IsEqualTo("Bearer tok");
    }

    [Test]
    public async Task Token_is_cached_between_calls() {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, Token);
        handler.Enqueue(HttpStatusCode.OK, "[]");
        handler.Enqueue(HttpStatusCode.OK, "[]");
        var client = Client(handler);

        await client.UsernameExistsAsync("a", CancellationToken.None);
        await client.UsernameExistsAsync("b", CancellationToken.None);

        await Assert.That(handler.Requests.Count).IsEqualTo(3);
    }

    [Test]
    public async Task UsernameExists_is_case_insensitive_exact() {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, Token);
        handler.Enqueue(HttpStatusCode.OK, """[{"id":"u1","username":"erika.mustermann","enabled":true}]""");

        var exists = await Client(handler).UsernameExistsAsync("Erika.Mustermann", CancellationToken.None);

        await Assert.That(exists).IsTrue();
        await Assert.That(handler.Requests[1].RequestUri!.Query).Contains("exact=true");
    }

    [Test]
    public async Task CreateUser_posts_credentials_and_attributes_and_returns_location_id() {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, Token);
        handler.EnqueueWithLocation(HttpStatusCode.Created, "https://sso.test/admin/realms/users/users/new-id");

        var id = await Client(handler).CreateUserAsync(new NewKeycloakUser("erika.mustermann", "Erika", "Mustermann", null, "Segel-Wolke-Hafen-47",
            new Dictionary<string, List<string>> { ["member_number"] = ["4711"] }), CancellationToken.None);

        await Assert.That(id).IsEqualTo("new-id");
        using var body = JsonDocument.Parse(handler.RequestBodies[1]);
        var credential = body.RootElement.GetProperty("credentials")[0];
        await Assert.That(credential.GetProperty("temporary").GetBoolean()).IsFalse();
        await Assert.That(credential.GetProperty("value").GetString()).IsEqualTo("Segel-Wolke-Hafen-47");
        await Assert.That(body.RootElement.GetProperty("emailVerified").GetBoolean()).IsFalse();
        await Assert.That(body.RootElement.TryGetProperty("email", out _)).IsFalse();
    }

    [Test]
    public async Task SetAttribute_puts_representation_without_username_and_email() {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, Token);
        handler.Enqueue(HttpStatusCode.OK, """{"id":"u1","username":"legacy name","email":"x@y.z","enabled":true,"attributes":{"member_number":["12"],"ekklesia_eligible":["false"],"other":["keep"]}}""");
        handler.Enqueue(HttpStatusCode.NoContent, "");

        await Client(handler).SetAttributeAsync("u1", "ekklesia_eligible", "true", CancellationToken.None);

        await Assert.That(handler.Requests[2].Method).IsEqualTo(HttpMethod.Put);
        using var body = JsonDocument.Parse(handler.RequestBodies[2]);
        await Assert.That(body.RootElement.TryGetProperty("username", out _)).IsFalse();
        await Assert.That(body.RootElement.TryGetProperty("email", out _)).IsFalse();
        var attributes = body.RootElement.GetProperty("attributes");
        await Assert.That(attributes.GetProperty("ekklesia_eligible")[0].GetString()).IsEqualTo("true");
        await Assert.That(attributes.GetProperty("other")[0].GetString()).IsEqualTo("keep");
    }

    [Test]
    public async Task Error_message_never_contains_the_request_body() {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, Token);
        handler.Enqueue(HttpStatusCode.Conflict, """{"errorMessage":"User exists with same username"}""");

        var ex = await Assert.ThrowsAsync<KeycloakException>(() => Client(handler).CreateUserAsync(
            new NewKeycloakUser("a", "A", "B", null, "Segel-Wolke-Hafen-47", []), CancellationToken.None));

        await Assert.That(ex!.StatusCode).IsEqualTo(409);
        await Assert.That(ex.Message).DoesNotContain("Segel-Wolke-Hafen-47");
    }

    [Test]
    public async Task Unconfigured_settings_report_not_configured() {
        await Assert.That(new KeycloakSettings { BaseUrl = "https://x", Realm = "r", ClientId = "c" }.IsConfigured).IsFalse();
        await Assert.That(Settings().IsConfigured).IsTrue();
    }
}
```

Add to `StubHttpMessageHandler` (shared fake):

```csharp
    /// <summary>Answers with <paramref name="status"/>, an empty body and a Location header, like Keycloak's create endpoints.</summary>
    public void EnqueueWithLocation(HttpStatusCode status, string location) {
        _responses.Enqueue(() => {
            var response = new HttpResponseMessage(status) { Content = new StringContent("") };
            response.Headers.Location = new Uri(location);
            return response;
        });
    }
```

- [ ] **Step 2: Run to verify failure** (build errors for missing types)

- [ ] **Step 3: Implement**

`KeycloakSettings.cs`:

```csharp
using Quartermaster.Data.Options;

namespace Quartermaster.Server.Accreditation;

public class KeycloakSettings {
    public string BaseUrl { get; init; } = "";
    public string AuthRealm { get; init; } = "master";
    public string Realm { get; init; } = "";
    public string ClientId { get; init; } = "";
    public string ClientSecret { get; init; } = "";
    public string UsernameSeparator { get; init; } = ".";

    public bool IsConfigured => BaseUrl.Length > 0 && Realm.Length > 0 && ClientId.Length > 0 && ClientSecret.Length > 0;

    public static KeycloakSettings ReadFrom(OptionRepository options) {
        string Value(string id, string fallback) {
            var value = options.GetGlobalValue(id)?.Value;
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        return new KeycloakSettings {
            BaseUrl = Value("keycloak.base_url", "").TrimEnd('/'),
            AuthRealm = Value("keycloak.auth_realm", "master"),
            Realm = Value("keycloak.realm", ""),
            ClientId = Value("keycloak.client_id", ""),
            ClientSecret = Value("keycloak.client_secret", ""),
            UsernameSeparator = Value("keycloak.username_separator", ".")
        };
    }
}
```

(`IsConfigured` has four simple length checks; CLAUDE.md allows up to four simple conditions inline.)

`KeycloakException.cs`:

```csharp
using System;

namespace Quartermaster.Server.Accreditation;

public class KeycloakException : Exception {
    public KeycloakException(int? statusCode, string message) : base(message) {
        StatusCode = statusCode;
    }

    public int? StatusCode { get; }
}
```

(Member order: move the property above the constructor.)

`KeycloakUserRepresentation.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Server.Accreditation;

public record KeycloakUserRepresentation(string Id, string Username, string? FirstName, string? LastName, string? Email, bool Enabled,
    Dictionary<string, List<string>> Attributes) {
    public string? Attribute(string name) => Attributes.TryGetValue(name, out var values) ? values.FirstOrDefault()?.Trim() : null;
}
```

`NewKeycloakUser.cs`:

```csharp
using System.Collections.Generic;

namespace Quartermaster.Server.Accreditation;

public record NewKeycloakUser(string Username, string FirstName, string LastName, string? Email, string Password,
    Dictionary<string, List<string>> Attributes);
```

`IKeycloakAdminClient.cs`:

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Quartermaster.Server.Accreditation;

public interface IKeycloakAdminClient {
    /// <summary>Accounts whose member_number equals <paramref name="memberNumber"/> exactly (trimmed), in any state.</summary>
    Task<List<KeycloakUserRepresentation>> FindByMemberNumberAsync(string memberNumber, CancellationToken ct);

    /// <summary>Case-insensitive exact username check.</summary>
    Task<bool> UsernameExistsAsync(string username, CancellationToken ct);

    /// <summary>Creates an enabled user with a final password; returns the new Keycloak user id.</summary>
    Task<string> CreateUserAsync(NewKeycloakUser user, CancellationToken ct);

    /// <summary>Sets one attribute to a single value, leaving every other attribute untouched.</summary>
    Task SetAttributeAsync(string userId, string name, string value, CancellationToken ct);
}
```

`KeycloakTokenProvider.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Quartermaster.Server.Accreditation;

/// <summary>Caches one client-credentials token per process until shortly before it expires.</summary>
public class KeycloakTokenProvider {
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromSeconds(10);

    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? token;
    private string tokenKey = "";
    private DateTimeOffset expiresAt;

    public async Task<string> GetTokenAsync(HttpClient http, KeycloakSettings settings, CancellationToken ct) {
        var key = $"{settings.BaseUrl}|{settings.AuthRealm}|{settings.ClientId}|{settings.ClientSecret}";
        await _lock.WaitAsync(ct);
        try {
            if (token != null && tokenKey == key && DateTimeOffset.UtcNow + RefreshMargin < expiresAt)
                return token;

            var url = $"{settings.BaseUrl}/realms/{Uri.EscapeDataString(settings.AuthRealm)}/protocol/openid-connect/token";
            using var form = new FormUrlEncodedContent(new Dictionary<string, string> {
                ["grant_type"] = "client_credentials",
                ["client_id"] = settings.ClientId,
                ["client_secret"] = settings.ClientSecret
            });
            using var response = await http.PostAsync(url, form, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                throw new KeycloakException((int)response.StatusCode, $"Keycloak token request returned {(int)response.StatusCode}");

            using var doc = JsonDocument.Parse(body);
            token = doc.RootElement.GetProperty("access_token").GetString();
            if (string.IsNullOrEmpty(token))
                throw new KeycloakException(null, "Keycloak token response without access_token");
            expiresAt = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(doc.RootElement.GetProperty("expires_in").GetInt32());
            tokenKey = key;
            return token;
        } finally {
            _lock.Release();
        }
    }
}
```

(The cache key includes the settings so an option change takes effect immediately; the secret is part of an in-memory key only and never logged.)

`HttpKeycloakAdminClient.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Quartermaster.Server.Accreditation;

public class HttpKeycloakAdminClient : IKeycloakAdminClient {
    private readonly HttpClient _http;
    private readonly KeycloakSettings _settings;
    private readonly KeycloakTokenProvider _tokens;

    public HttpKeycloakAdminClient(HttpClient http, KeycloakSettings settings, KeycloakTokenProvider tokens) {
        _http = http;
        _settings = settings;
        _tokens = tokens;
    }

    private string AdminBase => $"{_settings.BaseUrl}/admin/realms/{Uri.EscapeDataString(_settings.Realm)}";

    public async Task<List<KeycloakUserRepresentation>> FindByMemberNumberAsync(string memberNumber, CancellationToken ct) {
        var number = memberNumber.Trim();
        var url = $"{AdminBase}/users?q={Uri.EscapeDataString("member_number:" + number)}&briefRepresentation=false&max=10";
        var body = await SendAsync(HttpMethod.Get, url, null, ct);
        return ParseUsers(body)
            .Where(u => u.Attribute("member_number") == number)
            .ToList();
    }

    public async Task<bool> UsernameExistsAsync(string username, CancellationToken ct) {
        var url = $"{AdminBase}/users?username={Uri.EscapeDataString(username)}&exact=true";
        var body = await SendAsync(HttpMethod.Get, url, null, ct);
        return ParseUsers(body).Any(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<string> CreateUserAsync(NewKeycloakUser user, CancellationToken ct) {
        var json = new JsonObject {
            ["username"] = user.Username,
            ["firstName"] = user.FirstName,
            ["lastName"] = user.LastName,
            ["enabled"] = true,
            ["emailVerified"] = user.Email != null,
            ["attributes"] = ToJson(user.Attributes),
            ["credentials"] = new JsonArray(new JsonObject {
                ["type"] = "password",
                ["value"] = user.Password,
                ["temporary"] = false
            })
        };
        if (user.Email != null)
            json["email"] = user.Email;

        using var request = await CreateRequestAsync(HttpMethod.Post, $"{AdminBase}/users", json.ToJsonString(), ct);
        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw await ErrorAsync(response, "create user", ct);

        var location = response.Headers.Location?.ToString() ?? "";
        var id = location.Split('/').LastOrDefault() ?? "";
        if (id.Length == 0)
            throw new KeycloakException((int)response.StatusCode, "Keycloak created the user but returned no Location header");
        return id;
    }

    public async Task SetAttributeAsync(string userId, string name, string value, CancellationToken ct) {
        var url = $"{AdminBase}/users/{Uri.EscapeDataString(userId)}";
        var current = JsonNode.Parse(await SendAsync(HttpMethod.Get, url, null, ct))!.AsObject();
        // Keycloak 26 re-validates username and email on PUT, which rejects legacy usernames; leave both out.
        current.Remove("username");
        current.Remove("email");
        var attributes = current["attributes"] as JsonObject ?? new JsonObject();
        attributes[name] = new JsonArray(value);
        current["attributes"] = attributes;
        await SendAsync(HttpMethod.Put, url, current.ToJsonString(), ct);
    }

    private async Task<string> SendAsync(HttpMethod method, string url, string? jsonBody, CancellationToken ct) {
        using var request = await CreateRequestAsync(method, url, jsonBody, ct);
        HttpResponseMessage response;
        try {
            response = await _http.SendAsync(request, ct);
        } catch (OperationCanceledException) when (!ct.IsCancellationRequested) {
            throw new KeycloakException(null, "Keycloak timed out");
        }
        using (response) {
            if (!response.IsSuccessStatusCode)
                throw await ErrorAsync(response, $"{method} {new Uri(url).AbsolutePath}", ct);
            return await response.Content.ReadAsStringAsync(ct);
        }
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string url, string? jsonBody, CancellationToken ct) {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await _tokens.GetTokenAsync(_http, _settings, ct));
        if (jsonBody != null)
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        return request;
    }

    /// <summary>Builds the exception from the response only, never from the request, so a password in a request body cannot leak into messages or logs.</summary>
    private static async Task<KeycloakException> ErrorAsync(HttpResponseMessage response, string what, CancellationToken ct) {
        var body = await response.Content.ReadAsStringAsync(ct);
        var shortBody = body.Length <= 300 ? body : body[..300];
        return new KeycloakException((int)response.StatusCode, $"Keycloak {what} returned {(int)response.StatusCode}: {shortBody}");
    }

    private static JsonObject ToJson(Dictionary<string, List<string>> attributes) {
        var json = new JsonObject();
        foreach (var (key, values) in attributes)
            json[key] = new JsonArray(values.Select(v => (JsonNode?)JsonValue.Create(v)).ToArray());
        return json;
    }

    private static List<KeycloakUserRepresentation> ParseUsers(string body) {
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement
            .EnumerateArray()
            .Select(ParseUser)
            .ToList();
    }

    private static KeycloakUserRepresentation ParseUser(JsonElement e) {
        var attributes = new Dictionary<string, List<string>>();
        if (e.TryGetProperty("attributes", out var attrs) && attrs.ValueKind == JsonValueKind.Object) {
            foreach (var property in attrs.EnumerateObject()) {
                attributes[property.Name] = property.Value.ValueKind == JsonValueKind.Array
                    ? property.Value.EnumerateArray().Select(v => v.ToString()).ToList()
                    : [property.Value.ToString()];
            }
        }
        return new KeycloakUserRepresentation(
            e.GetProperty("id").GetString() ?? "",
            e.TryGetProperty("username", out var u) ? u.GetString() ?? "" : "",
            e.TryGetProperty("firstName", out var f) ? f.GetString() : null,
            e.TryGetProperty("lastName", out var l) ? l.GetString() : null,
            e.TryGetProperty("email", out var m) ? m.GetString() : null,
            e.TryGetProperty("enabled", out var en) && en.ValueKind == JsonValueKind.True,
            attributes);
    }
}
```

(`AdminBase` is a computed property; move it above the constructor per member order. `JsonDocument.Parse` failures on a 2xx surface as `JsonException`; wrap them: catch `JsonException`, `KeyNotFoundException`, `InvalidOperationException` around `ParseUsers` and the `JsonNode.Parse` in `SetAttributeAsync` and rethrow as `KeycloakException(null, "Keycloak returned an unexpected answer")`.)

Options (`OptionRepository.SupplementDefaults`, after the `newsposts.*` block):

```csharp
        AddDefinitionIfNotExists("keycloak.base_url",
            "Keycloak: Basis-URL",
            OptionDataType.String, false, "", "",
            description: "Basis-URL des Keycloak-Servers, z.B. https://sso.piratenpartei.de (für die Akkreditierung).");

        AddDefinitionIfNotExists("keycloak.auth_realm",
            "Keycloak: Realm des Admin-Clients",
            OptionDataType.String, false, "", "master");

        AddDefinitionIfNotExists("keycloak.realm",
            "Keycloak: Mitglieder-Realm",
            OptionDataType.String, false, "", "",
            description: "Realm, in dem die Mitgliederkonten liegen.");

        AddDefinitionIfNotExists("keycloak.client_id",
            "Keycloak: Client-ID",
            OptionDataType.String, false, "", "",
            description: "Client mit Client-Credentials und der Rolle manage-users im Mitglieder-Realm (derselbe Client wie beim Mitgliederdatenimport).");

        AddDefinitionIfNotExists("keycloak.client_secret",
            "Keycloak: Client-Secret",
            OptionDataType.String, false, "", "",
            isSecret: true);

        AddDefinitionIfNotExists("keycloak.username_separator",
            "Keycloak: Trennzeichen im Benutzernamen",
            OptionDataType.String, false, "", ".",
            description: "Trennzeichen zwischen Vor- und Nachname, wie beim Mitgliederdatenimport.");
```

`PermissionIdentifier.cs`:

```csharp
    /// <summary>Lets the holder look up member accounts in Keycloak, set voting eligibility, and create temporary accounts. Granted manually; not part of any default role.</summary>
    public static readonly string ManageAccreditation = "accreditation_manage";
```

(not added to `SystemPermissions` or any other list). `PermissionRepository.SupplementDefaults`: `AddIfNotExists(PermissionIdentifier.ManageAccreditation, "Akkreditierung durchführen", true);`

`Program.cs` (namespace-relative style like the existing News Posts lines):

```csharp
        builder.Services.AddSingleton<Accreditation.KeycloakTokenProvider>();
        builder.Services.AddScoped(sp => Accreditation.KeycloakSettings.ReadFrom(sp.GetRequiredService<OptionRepository>()));
        builder.Services.AddScoped<Accreditation.IKeycloakAdminClient>(sp => new Accreditation.HttpKeycloakAdminClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(),
            sp.GetRequiredService<Accreditation.KeycloakSettings>(),
            sp.GetRequiredService<Accreditation.KeycloakTokenProvider>()));
```

`Configuration.md`: section `### Keycloak (Akkreditierung)` with the six options in the file's table style and one sentence: the client needs the `manage-users` client role of `<realm>-realm` (or of `realm-management` when the client lives in the member realm), same client as MemberDataImport.

- [ ] **Step 4: Run tests** (`HttpKeycloakAdminClientTests`, `OptionRepositoryTests`): all pass.

- [ ] **Step 5: Checkpoint** (user commits)

---

### Task 2: Username and password generators

**Files:**
- Create: `Quartermaster.Server/Accreditation/AccreditationUsernameGenerator.cs`, `ReadablePasswordGenerator.cs`, `PasswordWords.cs`
- Test: `Quartermaster.Server.Tests/Accreditation/AccreditationUsernameGeneratorTests.cs`, `ReadablePasswordGeneratorTests.cs`

**Interfaces:**
- Produces:
  - `AccreditationUsernameGenerator.BuildBaseName(string firstName, string lastName, string separator) : string` (empty when nothing valid remains)
  - `AccreditationUsernameGenerator.HashSuffix(string memberNumber) : string`
  - `AccreditationUsernameGenerator.IsValid(string username) : bool`
  - `ReadablePasswordGenerator.Generate() : string`
  - `PasswordWords.All : IReadOnlyList<string>` (at least 300 entries)

- [ ] **Step 1: Write the failing tests**

```csharp
using Quartermaster.Server.Accreditation;

namespace Quartermaster.Server.Tests.Accreditation;

public class AccreditationUsernameGeneratorTests {
    [Test]
    [Arguments("Erika", "Mustermann", "Erika.Mustermann")]
    [Arguments("  Anna  Lena ", "von der Heide", "Anna.Lena.von.der.Heide")]
    [Arguments("Jörg", "Müller-Lüdenscheidt", "Jörg.Müller-Lüdenscheidt")]
    [Arguments("Dr. Max", "O'Neill (Gast)", "Dr.Max.ONeill.Gast")]
    public async Task BuildBaseName_matches_MemberDataImport(string first, string last, string expected) {
        await Assert.That(AccreditationUsernameGenerator.BuildBaseName(first, last, ".")).IsEqualTo(expected);
    }

    [Test]
    public async Task BuildBaseName_of_only_prohibited_characters_is_empty() {
        await Assert.That(AccreditationUsernameGenerator.BuildBaseName("$%", "!?", ".")).IsEqualTo("");
    }

    [Test]
    [Arguments("4711")]
    [Arguments(" 4711 ")]
    public async Task HashSuffix_is_six_digits_and_ignores_surrounding_whitespace(string number) {
        var suffix = AccreditationUsernameGenerator.HashSuffix(number);

        await Assert.That(suffix.Length).IsEqualTo(6);
        await Assert.That(suffix.All(char.IsAsciiDigit)).IsTrue();
        await Assert.That(suffix).IsEqualTo(AccreditationUsernameGenerator.HashSuffix("4711"));
    }

    [Test]
    [Arguments("Erika.Mustermann", true)]
    [Arguments("Erika Mustermann", false)]
    [Arguments("Erika/Mustermann", false)]
    [Arguments("", false)]
    public async Task IsValid_rejects_whitespace_prohibited_and_empty(string username, bool expected) {
        await Assert.That(AccreditationUsernameGenerator.IsValid(username)).IsEqualTo(expected);
    }
}
```

Before writing the implementation, compute the MemberDataImport reference value for `HashSuffix("4711")` by running MemberDataImport's own `UsernameGenerator.HashSuffix("4711")` (e.g. a scratch `dotnet run` against `/media/SMB/MemberDataImport` or a C# script with the same three lines) and add a test `HashSuffix_matches_MemberDataImport_reference` asserting that literal value. Do not modify the MemberDataImport repository.

```csharp
using System.Text.RegularExpressions;
using Quartermaster.Server.Accreditation;

namespace Quartermaster.Server.Tests.Accreditation;

public class ReadablePasswordGeneratorTests {
    [Test]
    public async Task Password_has_three_words_and_two_digits() {
        var password = ReadablePasswordGenerator.Generate();

        await Assert.That(Regex.IsMatch(password, "^[A-Z][a-z]+-[A-Z][a-z]+-[A-Z][a-z]+-[0-9]{2}$")).IsTrue();
        var words = password.Split('-').Take(3);
        await Assert.That(words.All(w => PasswordWords.All.Contains(w))).IsTrue();
    }

    [Test]
    public async Task Word_list_is_large_unique_and_ascii() {
        await Assert.That(PasswordWords.All.Count).IsGreaterThanOrEqualTo(300);
        await Assert.That(PasswordWords.All.Distinct().Count()).IsEqualTo(PasswordWords.All.Count);
        await Assert.That(PasswordWords.All.All(w => Regex.IsMatch(w, "^[A-Z][a-z]{2,7}$"))).IsTrue();
    }

    [Test]
    public async Task Draws_vary() {
        var draws = Enumerable.Range(0, 50).Select(_ => ReadablePasswordGenerator.Generate()).ToHashSet();

        await Assert.That(draws.Count).IsGreaterThan(45);
    }
}
```

- [ ] **Step 2: Run to verify failure**

- [ ] **Step 3: Implement**

`AccreditationUsernameGenerator.cs`:

```csharp
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Quartermaster.Server.Accreditation;

/// <summary>The username rule of MemberDataImport's UsernameGenerator; both must produce the same names so accounts look alike.</summary>
public static class AccreditationUsernameGenerator {
    private const string ProhibitedCharacters = "<>&\"'$%!#?§,;:*~/\\|^=[]{}()`";

    public static string BuildBaseName(string firstName, string lastName, string separator) {
        var raw = $"{firstName.Trim()} {lastName.Trim()}";
        var kept = new StringBuilder(raw.Length);
        foreach (var character in raw) {
            if (character == '.' || char.IsWhiteSpace(character)) {
                kept.Append(' ');
            } else if (!ProhibitedCharacters.Contains(character) && !char.IsControl(character)) {
                kept.Append(character);
            }
        }
        var parts = kept.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(separator, parts);
    }

    public static string HashSuffix(string memberNumber) {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(memberNumber.Trim()));
        var value = BinaryPrimitives.ReadUInt64BigEndian(hash.AsSpan(0, 8)) % 1_000_000;
        return value.ToString("D6", CultureInfo.InvariantCulture);
    }

    public static bool IsValid(string username) {
        if (username.Length == 0)
            return false;
        return !username.Any(c => char.IsWhiteSpace(c) || ProhibitedCharacters.Contains(c) || char.IsControl(c));
    }
}
```

(MemberDataImport joins first and last with the configured separator before normalizing; joining with a space and then using the separator for every gap gives the same result for both separators.)

`PasswordWords.cs`: `public static class PasswordWords { public static IReadOnlyList<string> All { get; } = [ ... ]; }` with at least 300 distinct, common, child-safe German nouns, capitalized, 3 to 8 ASCII letters, no umlauts or ß, no politically loaded or offensive words (examples: Anker, Apfel, Bach, Baum, Berg, Birne, Blatt, Boot, Brett, Brunnen, Buch, Burg, Dach, Deich, Dorf, Eiche, Eimer, Fass, Feder, Feld, Fenster, Fisch, Flagge, Flocke, Fluss, Gabel, Garten, Hafen, Hammer, Hecke, Himmel, Hut, Insel, Kamm, Kanne, Kerze, Kiesel, Kiste, Koffer, Korb, Kran, Lampe, Laterne, Leiter, Linde, Mantel, Mond). Keep the list alphabetically sorted so duplicates are easy to spot; the test rejects duplicates.

`ReadablePasswordGenerator.cs`:

```csharp
using System.Security.Cryptography;

namespace Quartermaster.Server.Accreditation;

public static class ReadablePasswordGenerator {
    public static string Generate() {
        var words = PasswordWords.All;
        var first = words[RandomNumberGenerator.GetInt32(words.Count)];
        var second = words[RandomNumberGenerator.GetInt32(words.Count)];
        var third = words[RandomNumberGenerator.GetInt32(words.Count)];
        var digits = RandomNumberGenerator.GetInt32(10, 100);
        return $"{first}-{second}-{third}-{digits}";
    }
}
```

- [ ] **Step 4: Run tests** (`AccreditationUsernameGeneratorTests`, `ReadablePasswordGeneratorTests`): all pass.

- [ ] **Step 5: Checkpoint** (user commits)

---

### Task 3: Endpoints

**Files:**
- Create: `Quartermaster.Api/Accreditation/AccreditationDTOs.cs`
- Create in `Quartermaster.Server/Accreditation/Endpoints/`: `AccreditationGuard.cs`, `AccreditationSearchEndpoint.cs`, `AccreditationSetEligibleEndpoint.cs`, `AccreditationUsernameSuggestionEndpoint.cs`, `AccreditationCreateTemporaryAccountEndpoint.cs`, `KeycloakAccountMapper.cs`, `KeycloakErrorMessages.cs`
- Test: `Quartermaster.Server.Tests/Accreditation/Fakes/FakeKeycloakAdminClient.cs`, `Quartermaster.Server.Tests/Accreditation/AccreditationEndpointTests.cs`

**Interfaces:**
- Consumes: Task 1 (`IKeycloakAdminClient`, `KeycloakSettings`, `KeycloakException`, `KeycloakUserRepresentation`, `NewKeycloakUser`, `PermissionIdentifier.ManageAccreditation`), Task 2 (generators).
- Produces routes:

```
GET  /api/accreditation/members/{MemberNumber}            List<AccreditationAccountDTO>
POST /api/accreditation/members/{MemberNumber}/eligible   AccreditationAccountDTO | 404 | 409
GET  /api/accreditation/username-suggestion?firstName=&lastName=&memberNumber=   UsernameSuggestionDTO
POST /api/accreditation/temporary-accounts                TemporaryAccountCreatedDTO | 400 | 409 (ExistingAccounts in body)
```

All return 503 with "Keycloak ist nicht konfiguriert." when `!settings.IsConfigured`, 502 with a German message on `KeycloakException`.

- [ ] **Step 1: DTOs and failing tests**

`AccreditationDTOs.cs`:

```csharp
using System.Collections.Generic;

namespace Quartermaster.Api.Accreditation;

public class AccreditationAccountDTO {
    public string KeycloakUserId { get; set; } = "";
    public string Username { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? Email { get; set; }
    public string MemberNumber { get; set; } = "";
    public bool Enabled { get; set; }
    public bool Closed { get; set; }
    public bool Eligible { get; set; }
    public bool Verified { get; set; }
}

public class UsernameSuggestionDTO {
    public string Username { get; set; } = "";
}

public class TemporaryAccountRequest {
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string MemberNumber { get; set; } = "";
    public string? Username { get; set; }
    public string? Email { get; set; }
    public bool Eligible { get; set; } = true;
}

public class TemporaryAccountCreatedDTO {
    public string KeycloakUserId { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

public class TemporaryAccountConflictDTO {
    public string Message { get; set; } = "";
    public List<AccreditationAccountDTO> ExistingAccounts { get; set; } = [];
}
```

`Fakes/FakeKeycloakAdminClient.cs`:

```csharp
using Quartermaster.Server.Accreditation;

namespace Quartermaster.Server.Tests.Accreditation.Fakes;

/// <summary>In-memory Keycloak; static because the web host resolves a new scoped instance per request. Tests call Reset() and run NotInParallel.</summary>
public class FakeKeycloakAdminClient : IKeycloakAdminClient {
    public static List<KeycloakUserRepresentation> Users { get; } = [];
    public static List<NewKeycloakUser> Created { get; } = [];
    public static KeycloakException? FailNext { get; set; }

    /// <summary>When set, CreateUserAsync also adds a second user with the same member number, simulating a concurrent create by another delegate.</summary>
    public static bool RaceOnCreate { get; set; }

    public static void Reset() {
        Users.Clear();
        Created.Clear();
        FailNext = null;
        RaceOnCreate = false;
    }

    public static KeycloakUserRepresentation AddUser(string id, string username, string memberNumber, bool enabled = true, string eligible = "false", bool closed = false) {
        var attributes = new Dictionary<string, List<string>> {
            ["member_number"] = [memberNumber],
            ["ekklesia_eligible"] = [eligible],
            ["ekklesia_verified"] = ["true"]
        };
        if (closed)
            attributes["closed_time"] = ["2026-10-01T02:00:00.000000+00:00"];
        var user = new KeycloakUserRepresentation(id, username, "Vor", "Nach", null, enabled, attributes);
        Users.Add(user);
        return user;
    }

    public Task<List<KeycloakUserRepresentation>> FindByMemberNumberAsync(string memberNumber, CancellationToken ct) {
        ThrowIfFailing();
        return Task.FromResult(Users.Where(u => u.Attribute("member_number") == memberNumber.Trim()).ToList());
    }

    public Task<bool> UsernameExistsAsync(string username, CancellationToken ct) {
        ThrowIfFailing();
        return Task.FromResult(Users.Any(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)));
    }

    public Task<string> CreateUserAsync(NewKeycloakUser user, CancellationToken ct) {
        ThrowIfFailing();
        Created.Add(user);
        var id = "kc-" + Guid.NewGuid();
        Users.Add(new KeycloakUserRepresentation(id, user.Username, user.FirstName, user.LastName, user.Email, true, user.Attributes));
        if (RaceOnCreate)
            AddUser("kc-" + Guid.NewGuid(), user.Username + ".other", user.Attributes["member_number"][0]);
        return Task.FromResult(id);
    }

    public Task SetAttributeAsync(string userId, string name, string value, CancellationToken ct) {
        ThrowIfFailing();
        var user = Users.Single(u => u.Id == userId);
        user.Attributes[name] = [value];
        return Task.CompletedTask;
    }

    private static void ThrowIfFailing() {
        if (FailNext == null)
            return;
        var failure = FailNext;
        FailNext = null;
        throw failure;
    }
}
```

`AccreditationEndpointTests.cs` (pattern: `PublishEndpointTests`; host built with `Factory.WithWebHostBuilder` replacing `IKeycloakAdminClient` with the fake and seeding the six `keycloak.*` options via `OptionRepository.SetValue` so `IsConfigured` is true; dispose the host in `[After(Test)]`; class marked `[NotInParallel("FakeKeycloakAdminClient")]`). Tests, each a separate `[Test]`:

1. Every route returns 401 anonymous and 403 for a user without `accreditation_manage` (use `[Arguments]` over method+URL; mutations with the CSRF header).
2. Search: 0 matches -> empty list; 1 match -> DTO with `Eligible=false`, `Closed=false`; a closed disabled account -> `Enabled=false`, `Closed=true`.
3. Set eligible: 1 match -> 200, fake attribute `ekklesia_eligible` now "true", audit row with `FieldName = "ekklesia_eligible"`, old "false", new "true"; 0 -> 404; 2 matches -> 409 and nothing changed.
4. Suggestion: free base name -> `Erika.Mustermann`; base taken -> `Erika.Mustermann.<HashSuffix("4711")>`.
5. Create: success returns username + a password matching the Task 2 regex; fake received attributes `member_number`, `ekklesia_verified`="true", `ekklesia_eligible`="true", `ekklesia_external_voting`="false", `created_time` matching `^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{6}\+00:00$`; `Eligible=false` in the request -> attribute "false".
6. Create refused (409) for a member number with an existing account, including a disabled closed one; body lists the existing account; nothing created.
7. Create with a manual username that exists -> 400; with an invalid manual username ("a b") -> 400; with no manual username and both base and suffixed names taken -> 400 asking for a manual username.
8. Create with `RaceOnCreate` -> 409 with message "Für diese Mitgliedsnummer gibt es jetzt mehrere Konten. Bitte die IT informieren." and an audit row for the created account (the account exists; it is reported, not deleted).
9. Password never in audit rows: after a successful create, no `AuditLogs` row's `OldValue`/`NewValue`/`FieldName` contains the returned password.
10. `FailNext = new KeycloakException(500, "boom")` on search -> 502 with "Keycloak meldet einen Fehler." and no stack trace in the body; 403 from Keycloak -> 502 "Quartermaster hat keine Berechtigung in Keycloak."; unconfigured options -> 503.
11. Member number validation: "abc", "", "0", "-1" -> 400 on search and create.

- [ ] **Step 2: Run to verify failure**

- [ ] **Step 3: Implement**

`AccreditationGuard.cs` keeps the repeated checks in one place:

```csharp
using System.Globalization;
using Quartermaster.Api;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Accreditation.Endpoints;

/// <summary>Shared request checks for the accreditation endpoints. Returns the HTTP status to send, or null when the request may proceed.</summary>
public static class AccreditationGuard {
    public static int? Check(PermissionContext perms, KeycloakSettings settings) {
        if (perms.UserId == null)
            return 401;
        if (!perms.HasGlobal(PermissionIdentifier.ManageAccreditation))
            return 403;
        if (!settings.IsConfigured)
            return 503;
        return null;
    }

    public static bool IsValidMemberNumber(string? value) {
        return int.TryParse(value?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > 0;
    }
}
```

`KeycloakErrorMessages.cs`:

```csharp
namespace Quartermaster.Server.Accreditation.Endpoints;

public static class KeycloakErrorMessages {
    public const string NotConfigured = "Keycloak ist nicht konfiguriert.";

    public static string For(KeycloakException ex) {
        if (ex.StatusCode == 401 || ex.StatusCode == 403)
            return "Quartermaster hat keine Berechtigung in Keycloak.";
        if (ex.StatusCode == 409)
            return "Benutzername oder E-Mail ist bereits vergeben.";
        if (ex.StatusCode == null)
            return "Keycloak ist nicht erreichbar.";
        return "Keycloak meldet einen Fehler.";
    }
}
```

`KeycloakAccountMapper.cs`: `public static AccreditationAccountDTO ToDto(KeycloakUserRepresentation user)` mapping fields; `Closed = user.Attribute("closed_time") != null`; `Eligible = user.Attribute("ekklesia_eligible") == "true"`; `Verified = user.Attribute("ekklesia_verified") == "true"`; `MemberNumber = user.Attribute("member_number") ?? ""`. A mapper (not a method on the record) because the record lives in the server and the DTO in the API project, and the record should stay a plain Keycloak model.

Endpoint shape (every endpoint injects `PermissionContext`, `KeycloakSettings`, `IKeycloakAdminClient`; uses `AccreditationGuard.Check` first and sends the returned status with `KeycloakErrorMessages.NotConfigured` for 503; wraps Keycloak calls in `try { ... } catch (KeycloakException ex) { Logger.LogWarning(ex, ...); AddError(KeycloakErrorMessages.For(ex)); await SendErrorsAsync(502, ct); }`):

- `AccreditationSearchEndpoint`: `Get("/api/accreditation/members/{MemberNumber}")`; 400 "Bitte eine gültige Mitgliedsnummer eingeben." when invalid; returns `FindByMemberNumberAsync` mapped.
- `AccreditationSetEligibleEndpoint`: `Post("/api/accreditation/members/{MemberNumber}/eligible")`; 0 matches -> 404 "Kein Konto mit dieser Mitgliedsnummer."; more than 1 -> 409 "Mehrere Konten mit dieser Mitgliedsnummer. Bitte die IT informieren."; otherwise `SetAttributeAsync(id, "ekklesia_eligible", "true")`, audit `Log("KeycloakAccount", Guid.Parse(id), "Updated", "ekklesia_eligible", old ?? "", "true")` (if the id is not a Guid, use `Guid.Empty` and put the id into `FieldName` as `ekklesia_eligible (keycloak id <id>)`), then re-read and return the DTO.
- `AccreditationUsernameSuggestionEndpoint`: `Get("/api/accreditation/username-suggestion")` with `[QueryParam]` firstName, lastName, memberNumber; empty base -> 200 with empty username; base free -> base; else base + separator + `HashSuffix`; if that is taken too -> empty username (the page then asks for manual entry).
- `AccreditationCreateTemporaryAccountEndpoint`: `Post("/api/accreditation/temporary-accounts")`, body `TemporaryAccountRequest`:
  1. Validate: first and last name non-blank, member number valid, email (when given and non-blank) contains `@` and no whitespace; 400 with German messages.
  2. `FindByMemberNumberAsync`; any result -> 409 with `TemporaryAccountConflictDTO` ("Für diese Mitgliedsnummer existiert bereits ein Konto. Bitte oben die Stimmberechtigung prüfen.") and the mapped accounts.
  3. Username: manual (trimmed) when given -> must be `IsValid` and not existing, else 400 ("Benutzername ist ungültig." / "Benutzername ist bereits vergeben."); otherwise the suggestion rule; empty result -> 400 "Bitte einen Benutzernamen eingeben.".
  4. Password `ReadablePasswordGenerator.Generate()`; attributes per Global Constraints; `CreateUserAsync`.
  5. Audit `Log("KeycloakAccount", <id as Guid>, "Created", "temporary_account", null, $"member {number}, username {username}, eligible {eligible}")`.
  6. Re-check `FindByMemberNumberAsync`; more than one -> 409 with the race message (the account stays; IT resolves). Otherwise 200 `TemporaryAccountCreatedDTO`.
  The password variable must never be passed to a logger, the audit log, or an exception message.

- [ ] **Step 4: Run tests** (`AccreditationEndpointTests`, plus Task 1 and Task 2 classes once more): all pass.

- [ ] **Step 5: Checkpoint** (user commits)

---

### Task 4: Page, navigation, i18n, changelog

**Files:**
- Create: `Quartermaster.Blazor/Pages/Administration/Accreditation.razor`, `Accreditation.razor.cs`
- Modify: `Quartermaster.Blazor/Layout/MainNavBar.razor`, `Quartermaster.Api/I18n/I18nKey.cs`, `Quartermaster.Server/wwwroot/i18n/de.json`, `en.json`, `Quartermaster.Server/Changelog/V3.md`

**Interfaces:**
- Consumes: Task 3 routes and DTOs; JS function `CopyToClipboard(text)` from `wwwroot/js/clipboard.js` (already loaded; call via `IJSRuntime.InvokeAsync<bool>("CopyToClipboard", text)`, as other pages do).

- [ ] **Step 1: i18n keys** under `I18nKey.Ui.Accreditation` (prefix `ui.accreditation.`) plus `Ui.MainNavBar.BoardWorkAccreditation = "ui.main_nav_bar.board_work.accreditation"`. German values (English equivalents in `en.json`):

```
page_title                 Akkreditierung
not_configured             Keycloak ist nicht konfiguriert. Bitte die Keycloak-Einstellungen hinterlegen.
eligibility_title          Stimmberechtigung freischalten
eligibility_intro          Für Mitglieder, die nach dem morgendlichen Abgleich bezahlt haben. Gilt bis zum nächsten nächtlichen Abgleich.
member_number              Mitgliedsnummer
search                     Suchen
no_account                 Kein Konto mit dieser Mitgliedsnummer. Unten kann ein temporärer Zugang angelegt werden.
multiple_accounts          Mehrere Konten mit dieser Mitgliedsnummer. Bitte die IT informieren.
username                   Benutzername
name                       Name
email                      E-Mail
status                     Status
status_active              aktiv
status_disabled            deaktiviert
status_closed              geschlossen
eligible                   Stimmberechtigt
verified                   Verifiziert
yes                        Ja
no                         Nein
already_eligible           Dieses Konto ist bereits stimmberechtigt.
disabled_warning           Dieses Konto ist deaktiviert; die Person kann sich damit nicht anmelden.
set_eligible               Stimmberechtigt setzen
set_eligible_confirm       Stimmrecht für {name} (Nr. {number}) setzen?
set_eligible_done          Stimmberechtigung gesetzt.
temporary_title            Temporären Zugang anlegen
temporary_intro            Für Mitglieder ohne Konto (z.B. ohne E-Mail-Adresse). Der Zugang wird in der kommenden Nacht automatisch geschlossen.
first_name                 Vorname
last_name                  Nachname
username_hint              Wird aus Vor- und Nachname vorgeschlagen und kann geändert werden.
email_optional             E-Mail (optional)
create                     Zugang anlegen
created_title              Zugang angelegt
password                   Passwort
copy                       Kopieren
copied                     Kopiert.
created_note               Wird nur jetzt angezeigt; Zugang wird in der kommenden Nacht automatisch geschlossen.
existing_account           Für diese Mitgliedsnummer existiert bereits ein Konto. Bitte oben die Stimmberechtigung prüfen.
```

Nav: `ui.main_nav_bar.board_work.accreditation` = "Akkreditierung".

- [ ] **Step 2: Navigation.** In `MainNavBar.razor`, add `PermissionIdentifier.ManageAccreditation` to the outer "Vorstandsarbeit" `AnyOfPermissions` list (next to `ReviewNewsPosts`) and add inside the dropdown:

```razor
                                <RequirePermission GlobalPermission="@PermissionIdentifier.ManageAccreditation">
                                    <li><hr class="dropdown-divider"></li>
                                    <li><a class="dropdown-item" href="/Administration/Akkreditierung">@I18n[I18nKey.Ui.MainNavBar.BoardWorkAccreditation]</a></li>
                                </RequirePermission>
```

- [ ] **Step 3: Page.** `Accreditation.razor` (`@page "/Administration/Akkreditierung"`, wrapped in `<RequirePermission GlobalPermission="@PermissionIdentifier.ManageAccreditation">`), two cards:

1. Eligibility card: member number input + "Suchen" (Enter submits); results:
   - none -> `no_account`;
   - several -> list of username/name/status rows + `multiple_accounts`, no button;
   - one -> definition list (username, name, email, status, eligible, verified); `already_eligible` note or the "Stimmberechtigt setzen" button (with `disabled_warning` alert above it for disabled accounts); the button opens `ConfirmDialog` with `set_eligible_confirm` (replace `{name}`/`{number}`), POSTs, shows `set_eligible_done` toast and re-renders the returned DTO.
   - A 503 shows `not_configured` as an alert instead of the results.
2. Temporary account card: inputs Vorname, Nachname, Mitgliedsnummer, Benutzername (with `username_hint`), E-Mail (optional), checkbox Stimmberechtigt (default checked), each with a `<label for>`.
   - When first name, last name and member number are all filled and the delegate has not edited the username, the page requests `/api/accreditation/username-suggestion` (debounced: on change/blur of those three fields, not per keystroke) and fills the username. Track a `usernameEdited` flag set by the username input's `@oninput`.
   - "Zugang anlegen" (disabled while busy) POSTs; 409 with `ExistingAccounts` shows `existing_account` plus the accounts; other errors show a toast via `ToastService.ErrorAsync(response)`.
   - On success, a success card shows username and password in a large monospace font, a "Kopieren" button (copies `Benutzername: <u>` + newline + `Passwort: <p>`), and `created_note`; the form fields reset (checkbox back to checked, `usernameEdited` false). The created card stays until the next creation or until the delegate dismisses it; the password is kept only in a component field, never in local storage.

Code-behind follows `NewsPostsSettings.razor.cs`: injected `HttpClient`, `I18nService`, `ToastService`, `IJSRuntime`; a `busy` flag around every request with try/catch(HttpRequestException) -> `ToastService.Error(ex)`; `async Task` handlers only (no fire-and-forget).

- [ ] **Step 4: Changelog.** Append to `V3.md`:

```markdown

### Akkreditierung

Unter "Vorstandsarbeit > Akkreditierung" lässt sich für Online-Parteitage die Stimmberechtigung eines Mitglieds nachträglich freischalten und ein temporärer Zugang für Mitglieder ohne Konto anlegen.
```

- [ ] **Step 5: Verify.** Build; validate `de.json`/`en.json` with a short python check (valid JSON, identical new key sets); run `AccreditationEndpointTests` once. Do not start or restart the dev server (the controller does that). Checkpoint (user commits).

---

## Manual end-to-end check (user)

1. Set the six `keycloak.*` options (same client as MemberDataImport) and grant yourself `accreditation_manage`.
2. Akkreditierung page: search a known member number; the card must show the right person.
3. Create a temporary account for an unused test member number (e.g. 999999) without email; log in to OpenSlides (or the Keycloak account console) with the shown credentials.
4. Set eligibility on an account with `ekklesia_eligible=false` and confirm the attribute in the Keycloak console.
5. Delete the test account in Keycloak afterwards (or let the next nightly run close it).
