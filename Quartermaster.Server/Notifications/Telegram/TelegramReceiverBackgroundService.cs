using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace Quartermaster.Server.Notifications.Telegram;

/// <summary>
/// Long-polls Telegram for updates and routes each one through
/// <see cref="TelegramUpdateHandler"/>. Idle (no client created) until a bot token
/// appears in options, then polls indefinitely. Per-iteration scope so each handler
/// invocation gets a fresh DbContext.
/// </summary>
public class TelegramReceiverBackgroundService : BackgroundService {
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);
    private const int LongPollSeconds = 25;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TelegramReceiverBackgroundService> _logger;

    public TelegramReceiverBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<TelegramReceiverBackgroundService> logger) {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct) {
        var offset = 0;
        while (!ct.IsCancellationRequested) {
            try {
                offset = await PollOnceAsync(offset, ct);
            } catch (OperationCanceledException) when (ct.IsCancellationRequested) {
                return;
            } catch (Exception ex) {
                _logger.LogWarning(ex, "Telegram receiver poll failed; retrying in {Delay}", RetryDelay);
                await Task.Delay(RetryDelay, ct);
            }
        }
    }

    private async Task<int> PollOnceAsync(int offset, CancellationToken ct) {
        ITelegramBotClient? bot;
        using (var lookupScope = _scopeFactory.CreateScope()) {
            bot = lookupScope.ServiceProvider.GetRequiredService<TelegramBotClientFactory>().CreateOrNull();
        }
        if (bot == null) {
            await Task.Delay(RetryDelay, ct);
            return offset;
        }

        var updates = await bot.GetUpdates(
            offset: offset,
            timeout: LongPollSeconds,
            allowedUpdates: new[] { UpdateType.Message },
            cancellationToken: ct);
        if (updates.Length == 0) {
            return offset;
        }

        using var handlerScope = _scopeFactory.CreateScope();
        var handler = handlerScope.ServiceProvider.GetRequiredService<TelegramUpdateHandler>();
        var now = DateTime.UtcNow;
        foreach (var update in updates) {
            if (update.Id >= offset) {
                offset = update.Id + 1;
            }
            try {
                await handler.HandleAsync(bot, update, now, ct);
            } catch (Exception ex) {
                _logger.LogWarning(ex, "Telegram update handler failed for update {UpdateId}", update.Id);
            }
        }
        return offset;
    }
}
