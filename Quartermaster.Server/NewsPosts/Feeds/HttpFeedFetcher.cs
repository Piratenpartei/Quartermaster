using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Quartermaster.Server.NewsPosts.Feeds;

public class HttpFeedFetcher : IFeedFetcher {
    private const string UserAgent = "Quartermaster/3 (+https://piratenpartei.de)";

    private readonly HttpClient _http;

    public HttpFeedFetcher(HttpClient http) {
        _http = http;
    }

    public async Task<string> GetStringAsync(string url, CancellationToken ct) {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }
}
