using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.AssemblyMotions.Forum;

/// <summary>Polls the forum for replies to mirrored motions at the interval set in <c>discourse.reply_poll_minutes</c>; per-iteration errors don't kill the service.</summary>
public class AssemblyMotionForumReplyHostedService : BackgroundService {
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AssemblyMotionForumReplyHostedService> _logger;

    public AssemblyMotionForumReplyHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<AssemblyMotionForumReplyHostedService> logger) {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        _logger.LogInformation("Assembly motion forum reply hosted service started");
        while (!stoppingToken.IsCancellationRequested) {
            var interval = TimeSpan.FromMinutes(DiscourseSettings.DefaultReplyPollMinutes);
            try {
                using var scope = _scopeFactory.CreateScope();
                interval = DiscourseSettings.ReplyPollInterval(scope.ServiceProvider.GetRequiredService<OptionRepository>());
                var poll = scope.ServiceProvider.GetRequiredService<AssemblyMotionForumReplyPoll>();
                var notified = await poll.PollAsync(DateTime.UtcNow, stoppingToken);
                if (notified > 0)
                    _logger.LogInformation("Notified {Count} forum replies to assembly motion authors", notified);
            } catch (OperationCanceledException) {
                break;
            } catch (Exception ex) {
                _logger.LogError(ex, "Assembly motion forum reply poll failed");
            }
            try {
                await Task.Delay(interval, stoppingToken);
            } catch (OperationCanceledException) {
                break;
            }
        }
    }
}
