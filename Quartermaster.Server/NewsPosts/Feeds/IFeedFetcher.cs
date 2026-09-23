using System.Threading;
using System.Threading.Tasks;

namespace Quartermaster.Server.NewsPosts.Feeds;

public interface IFeedFetcher {
    Task<string> GetStringAsync(string url, CancellationToken ct);
}
