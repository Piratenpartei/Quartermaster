using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Quartermaster.Server.NewsPosts.Programme;

public interface IWikiClient {
    Task<WikiPage> GetPageAsync(string title, CancellationToken ct);

    /// <summary>Lists main-namespace page titles starting with <paramref name="prefix"/> (e.g. <c>Positionspapiere/</c>).</summary>
    Task<List<string>> ListSubpagesAsync(string prefix, CancellationToken ct);
}
