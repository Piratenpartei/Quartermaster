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

    public async Task<string> GetTokenAsync(HttpClient http, KeycloakSettings settings, CancellationToken ct, bool forceRefresh = false) {
        var key = $"{settings.BaseUrl}|{settings.AuthRealm}|{settings.ClientId}|{settings.ClientSecret}";
        await _lock.WaitAsync(ct);
        try {
            if (!forceRefresh && token != null && tokenKey == key && DateTimeOffset.UtcNow + RefreshMargin < expiresAt)
                return token;

            var url = $"{settings.BaseUrl}/realms/{Uri.EscapeDataString(settings.AuthRealm)}/protocol/openid-connect/token";
            using var form = new FormUrlEncodedContent(new Dictionary<string, string> {
                ["grant_type"] = "client_credentials",
                ["client_id"] = settings.ClientId,
                ["client_secret"] = settings.ClientSecret
            });
            HttpResponseMessage response;
            try {
                response = await http.PostAsync(url, form, ct);
            } catch (HttpRequestException ex) {
                throw new KeycloakException(null, "Keycloak is unreachable", ex);
            }
            using (response) {
                var body = await response.Content.ReadAsStringAsync(ct);
                if (!response.IsSuccessStatusCode)
                    throw new KeycloakException((int)response.StatusCode, $"Keycloak token request returned {(int)response.StatusCode}");

                var (newToken, expiresInSeconds) = ParseTokenResponse(body);
                token = newToken;
                expiresAt = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(expiresInSeconds);
                tokenKey = key;
                return newToken;
            }
        } finally {
            _lock.Release();
        }
    }

    private static (string Token, int ExpiresInSeconds) ParseTokenResponse(string body) {
        try {
            using var doc = JsonDocument.Parse(body);
            var parsedToken = doc.RootElement.GetProperty("access_token").GetString();
            if (string.IsNullOrEmpty(parsedToken))
                throw new KeycloakException(null, "Keycloak token response without access_token");
            return (parsedToken, doc.RootElement.GetProperty("expires_in").GetInt32());
        } catch (Exception ex) when (ex is JsonException || ex is KeyNotFoundException || ex is InvalidOperationException || ex is FormatException) {
            throw new KeycloakException(null, "Keycloak token response has an unexpected shape", ex);
        }
    }
}
