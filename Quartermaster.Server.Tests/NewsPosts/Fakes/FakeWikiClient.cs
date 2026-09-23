using Quartermaster.Server.NewsPosts.Programme;

namespace Quartermaster.Server.Tests.NewsPosts.Fakes;

public class FakeWikiClient : IWikiClient {
    public Dictionary<string, WikiPage> Pages { get; } = [];
    public HashSet<string> Failing { get; } = [];

    /// <summary>When set, <see cref="GetPageAsync"/> waits on it before returning, so a test can hold a sync
    /// "in flight" to exercise overlapping calls deterministically.</summary>
    public TaskCompletionSource? Gate { get; set; }

    /// <summary>Completes as soon as <see cref="GetPageAsync"/> is entered, so a test can wait for the first call
    /// to actually be inside the gated section (and holding the sync lock) before starting a second one, instead
    /// of guessing with a delay.</summary>
    public TaskCompletionSource? Started { get; set; }

    public async Task<WikiPage> GetPageAsync(string title, CancellationToken ct) {
        Started?.TrySetResult();
        if (Gate != null)
            await Gate.Task;
        if (Failing.Contains(title))
            throw new HttpRequestException("wiki down");
        return Pages[title];
    }

    public Task<List<string>> ListSubpagesAsync(string prefix, CancellationToken ct)
        => Task.FromResult(Pages.Keys.Where(k => k.StartsWith(prefix)).OrderBy(k => k).ToList());
}
