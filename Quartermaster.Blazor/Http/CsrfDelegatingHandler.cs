using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Http;

public class CsrfDelegatingHandler : DelegatingHandler {
    private string? _csrfToken;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) {

        // Wait for auth initialization before sending requests (prevents race condition on page reload)
        if (!AuthService.Initialized)
            await AuthService.WaitForInitialization;

        var authToken = AuthService.StaticToken;
        if (!string.IsNullOrEmpty(authToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

        if (request.Method != HttpMethod.Get &&
            request.Method != HttpMethod.Head &&
            request.Method != HttpMethod.Options) {

            if (_csrfToken == null)
                await FetchTokenAsync(request, cancellationToken);

            if (_csrfToken != null) {
                request.Headers.Remove("X-CSRF-TOKEN");
                request.Headers.Add("X-CSRF-TOKEN", _csrfToken);
            }
        }

        var response = await base.SendAsync(request, cancellationToken);

        // If we get a 401, token expired or invalid — clear auth state and notify UI
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            AuthService.NotifyTokenExpired();

        // If we get a 403, CSRF token may have expired — refetch and retry once
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden &&
            request.Method != HttpMethod.Get) {

            _csrfToken = null;
            await FetchTokenAsync(request, cancellationToken);

            if (_csrfToken != null) {
                request.Headers.Remove("X-CSRF-TOKEN");
                request.Headers.Add("X-CSRF-TOKEN", _csrfToken);
                response = await base.SendAsync(request, cancellationToken);
            }
        }

        return response;
    }

    private async Task FetchTokenAsync(HttpRequestMessage originalRequest, CancellationToken ct) {
        var baseUri = originalRequest.RequestUri?.GetLeftPart(UriPartial.Authority) ?? "";
        var tokenRequest = new HttpRequestMessage(HttpMethod.Get, $"{baseUri}/api/antiforgery/token");
        var response = await base.SendAsync(tokenRequest, ct);

        if (response.IsSuccessStatusCode) {
            var result = await response.Content.ReadFromJsonAsync<AntiforgeryTokenResponse>(ct);
            _csrfToken = result?.Token;
        }
    }

    private class AntiforgeryTokenResponse {
        public string? Token { get; set; }
    }
}
