using Quartermaster.Server.NewsPosts.Feeds;

namespace Quartermaster.Server.Tests.NewsPosts.Fakes;

public class FakeFeedFetcher : IFeedFetcher {
    public Dictionary<string, string> Responses { get; } = [];
    public HashSet<string> CancelledUrls { get; } = [];

    public Task<string> GetStringAsync(string url, CancellationToken ct) {
        if (CancelledUrls.Contains(url))
            throw new OperationCanceledException(ct);
        if (!Responses.TryGetValue(url, out var body))
            throw new HttpRequestException("404 " + url);
        return Task.FromResult(body);
    }

    public static string Load(string file) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "NewsPosts", file));
}
