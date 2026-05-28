using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Quartermaster.Rendering;

namespace Quartermaster.Server.Rendering;

/// <summary>
/// Warms the Markdown → sanitized-HTML pipeline at startup so the first request that
/// renders markdown (e.g. submitting a motion) doesn't pay the ~1s AngleSharp/Markdig
/// first-use initialization. Runs in <see cref="StartAsync"/> so it completes before
/// the host reports ready.
/// </summary>
public class MarkdownWarmupHostedService : IHostedService {
    public Task StartAsync(CancellationToken cancellationToken) {
        MarkdownService.Warmup();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
