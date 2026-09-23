using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Quartermaster.Server.NewsPosts.Programme;

public class HttpWikiClient : IWikiClient {
    private const string ApiUrl = "https://wiki.piratenpartei.de/api.php";

    /// <summary>The wiki's Anubis bot check challenges browser-like user agents, also on api.php; a plain tool UA gets JSON.</summary>
    private const string UserAgent = "Quartermaster/3 (+https://piratenpartei.de)";

    private readonly HttpClient _http;

    public HttpWikiClient(HttpClient http) {
        _http = http;
    }

    public async Task<WikiPage> GetPageAsync(string title, CancellationToken ct) {
        var url = $"{ApiUrl}?action=parse&page={Uri.EscapeDataString(title)}&prop=wikitext%7Crevid&format=json";
        using var doc = await GetJsonAsync(url, ct);
        if (doc.RootElement.TryGetProperty("error", out var error))
            throw new HttpRequestException($"Wiki error for '{title}': {error.GetProperty("info").GetString()}");

        var parse = doc.RootElement.GetProperty("parse");
        return new WikiPage(
            parse.GetProperty("title").GetString() ?? title,
            parse.GetProperty("revid").GetInt64(),
            parse.GetProperty("wikitext").GetProperty("*").GetString() ?? "");
    }

    public async Task<List<string>> ListSubpagesAsync(string prefix, CancellationToken ct) {
        var titles = new List<string>();
        string? continueFrom = null;
        do {
            var url = $"{ApiUrl}?action=query&list=allpages&apnamespace=0&aplimit=max&format=json&apprefix={Uri.EscapeDataString(prefix)}";
            if (continueFrom != null)
                url += "&apcontinue=" + Uri.EscapeDataString(continueFrom);

            using var doc = await GetJsonAsync(url, ct);
            foreach (var page in doc.RootElement.GetProperty("query").GetProperty("allpages").EnumerateArray())
                titles.Add(page.GetProperty("title").GetString() ?? "");

            continueFrom = doc.RootElement.TryGetProperty("continue", out var cont)
                ? cont.GetProperty("apcontinue").GetString()
                : null;
        } while (continueFrom != null);
        return titles;
    }

    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken ct) {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(ct);
        if (body.TrimStart().StartsWith('<'))
            throw new HttpRequestException("Wiki returned HTML instead of JSON (bot check?)");
        return JsonDocument.Parse(body);
    }
}
