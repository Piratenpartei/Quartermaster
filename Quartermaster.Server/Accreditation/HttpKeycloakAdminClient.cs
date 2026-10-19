using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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

    private string AdminBase => $"{_settings.BaseUrl}/admin/realms/{Uri.EscapeDataString(_settings.Realm)}";

    public HttpKeycloakAdminClient(HttpClient http, KeycloakSettings settings, KeycloakTokenProvider tokens) {
        _http = http;
        _settings = settings;
        _tokens = tokens;
    }

    public async Task<List<KeycloakUserRepresentation>> FindByMemberNumberAsync(string memberNumber, CancellationToken ct) {
        var number = memberNumber.Trim();
        // Keycloak's q= match can be looser than exact; a generous page keeps the client-side exact filter below from missing the real match behind non-exact candidates.
        var url = $"{AdminBase}/users?q={Uri.EscapeDataString("member_number:" + number)}&briefRepresentation=false&max=100";
        var body = await SendAsync(HttpMethod.Get, url, null, ct);
        return ParseUsersOrThrow(body)
            .Where(u => u.Attribute("member_number") == number)
            .ToList();
    }

    public async Task<List<KeycloakUserRepresentation>> SearchAsync(string text, int max, CancellationToken ct) {
        var url = $"{AdminBase}/users?search={Uri.EscapeDataString("*" + text.Trim() + "*")}&briefRepresentation=false&max={max}";
        return ParseUsersOrThrow(await SendAsync(HttpMethod.Get, url, null, ct));
    }

    public async Task<KeycloakUserRepresentation?> GetUserAsync(string userId, CancellationToken ct) {
        var url = $"{AdminBase}/users/{Uri.EscapeDataString(userId)}";
        using var response = await SendWithRetryAsync(HttpMethod.Get, url, null, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        if (!response.IsSuccessStatusCode)
            throw await ErrorAsync(response, "get user", ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        try {
            using var doc = JsonDocument.Parse(body);
            return ParseUser(doc.RootElement);
        } catch (Exception ex) when (ex is JsonException || ex is KeyNotFoundException || ex is InvalidOperationException) {
            throw new KeycloakException(null, "Keycloak returned an unexpected answer", ex);
        }
    }

    public async Task<bool> UsernameExistsAsync(string username, CancellationToken ct) {
        var url = $"{AdminBase}/users?username={Uri.EscapeDataString(username)}&exact=true";
        var body = await SendAsync(HttpMethod.Get, url, null, ct);
        return ParseUsersOrThrow(body).Any(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
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

        using var response = await SendWithRetryAsync(HttpMethod.Post, $"{AdminBase}/users", json.ToJsonString(), ct);
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
        var current = ParseObjectOrThrow(await SendAsync(HttpMethod.Get, url, null, ct));
        // Keycloak reports a brute-force-locked user as enabled:false; sending that back would turn a temporary lock into a permanent disable.
        current.Remove("enabled");
        var attributes = current["attributes"] as JsonObject ?? new JsonObject();
        attributes[name] = new JsonArray(value);
        current["attributes"] = attributes;
        await SendAsync(HttpMethod.Put, url, current.ToJsonString(), ct);
    }

    public async Task CheckAccessAsync(CancellationToken ct) {
        await SendAsync(HttpMethod.Get, $"{AdminBase}/users?max=1&briefRepresentation=true", null, ct);
    }

    private async Task<string> SendAsync(HttpMethod method, string url, string? jsonBody, CancellationToken ct) {
        using var response = await SendWithRetryAsync(method, url, jsonBody, ct);
        if (!response.IsSuccessStatusCode)
            throw await ErrorAsync(response, $"{method} {new Uri(url).AbsolutePath}", ct);
        return await response.Content.ReadAsStringAsync(ct);
    }

    // Keycloak answers 401 when the cached token expired early or was revoked; one retry with a freshly
    // requested token covers that without surfacing a spurious failure to the caller.
    private async Task<HttpResponseMessage> SendWithRetryAsync(HttpMethod method, string url, string? jsonBody, CancellationToken ct) {
        var response = await SendOnceAsync(method, url, jsonBody, forceRefresh: false, ct);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        response.Dispose();
        return await SendOnceAsync(method, url, jsonBody, forceRefresh: true, ct);
    }

    private async Task<HttpResponseMessage> SendOnceAsync(HttpMethod method, string url, string? jsonBody, bool forceRefresh, CancellationToken ct) {
        using var request = await CreateRequestAsync(method, url, jsonBody, forceRefresh, ct);
        try {
            return await _http.SendAsync(request, ct);
        } catch (OperationCanceledException ex) when (!ct.IsCancellationRequested) {
            throw new KeycloakException(null, "Keycloak timed out", ex);
        } catch (HttpRequestException ex) {
            throw new KeycloakException(null, "Keycloak is unreachable", ex);
        }
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string url, string? jsonBody, bool forceRefresh, CancellationToken ct) {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await _tokens.GetTokenAsync(_http, _settings, ct, forceRefresh));
        if (jsonBody != null)
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        return request;
    }

    // Built from the response only, never from the request, so a password in a request body cannot leak into messages or logs.
    private static async Task<KeycloakException> ErrorAsync(HttpResponseMessage response, string what, CancellationToken ct) {
        var body = await response.Content.ReadAsStringAsync(ct);
        var shortBody = body.Length <= 300 ? body : body[..300];
        var errorMessageKey = ParseErrorMessageKey(body);
        return new KeycloakException((int)response.StatusCode, $"Keycloak {what} returned {(int)response.StatusCode}: {shortBody}", null, errorMessageKey);
    }

    // Keycloak never echoes the password in errorMessage, but the body shape otherwise is not guaranteed; a parse failure
    // here must fall back to "no key" rather than mask the original error with a JSON exception.
    private static string? ParseErrorMessageKey(string body) {
        try {
            return JsonNode.Parse(body)?.AsObject().TryGetPropertyValue("errorMessage", out var value) == true
                ? value?.GetValue<string>()
                : null;
        } catch (Exception ex) when (ex is JsonException || ex is InvalidOperationException) {
            return null;
        }
    }

    private static JsonObject ToJson(Dictionary<string, List<string>> attributes) {
        var json = new JsonObject();
        foreach (var (key, values) in attributes)
            json[key] = new JsonArray(values.Select(v => (JsonNode?)JsonValue.Create(v)).ToArray());
        return json;
    }

    // Keycloak answered 2xx, but an unexpected shape (missing field, wrong type) must still surface as a KeycloakException rather than an unhandled JsonException.
    private static List<KeycloakUserRepresentation> ParseUsersOrThrow(string body) {
        try {
            return ParseUsers(body);
        } catch (Exception ex) when (ex is JsonException || ex is KeyNotFoundException || ex is InvalidOperationException) {
            throw new KeycloakException(null, "Keycloak returned an unexpected answer", ex);
        }
    }

    private static JsonObject ParseObjectOrThrow(string body) {
        try {
            return JsonNode.Parse(body)!.AsObject();
        } catch (Exception ex) when (ex is JsonException || ex is KeyNotFoundException || ex is InvalidOperationException) {
            throw new KeycloakException(null, "Keycloak returned an unexpected answer", ex);
        }
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
