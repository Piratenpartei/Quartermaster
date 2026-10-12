using System.Threading;
using System.Threading.Tasks;
namespace Quartermaster.Server.AssemblyMotions.Wiki;

public interface IWikiBotClient {
    Task LoginAsync(WikiBotSettings settings, CancellationToken ct);

    /// <summary>Returns the wikitext of the page, or null when the page does not exist.</summary>
    Task<string?> GetContentAsync(WikiBotSettings settings, string title, CancellationToken ct);

    Task EditAsync(WikiBotSettings settings, string title, string text, string summary, CancellationToken ct);

    Task ProtectAsync(WikiBotSettings settings, string title, CancellationToken ct);
}
