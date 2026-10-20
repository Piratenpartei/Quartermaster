using System.Threading;
using System.Threading.Tasks;
namespace Quartermaster.Server.AssemblyMotions.Wiki;

public interface IWikiBotClient {
    /// <summary>
    /// How the wiki addresses its pages, as an absolute URL with <c>$1</c> for the title (for example https://wiki.example.org/wiki/$1).
    /// Read from the wiki's site info, so short URLs and a script folder elsewhere than the API both work.
    /// </summary>
    Task<string> GetArticlePathAsync(WikiBotSettings settings, CancellationToken ct);

    Task LoginAsync(WikiBotSettings settings, CancellationToken ct);

    /// <summary>Returns the wikitext of the page, or null when the page does not exist.</summary>
    Task<string?> GetContentAsync(WikiBotSettings settings, string title, CancellationToken ct);

    Task EditAsync(WikiBotSettings settings, string title, string text, string summary, CancellationToken ct);

    Task ProtectAsync(WikiBotSettings settings, string title, CancellationToken ct);
}
