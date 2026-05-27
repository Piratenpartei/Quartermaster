using Quartermaster.Data.Options;
using Telegram.Bot;

namespace Quartermaster.Server.Notifications.Telegram;

/// <summary>
/// Reads the current bot token from <see cref="OptionRepository"/> and hands the
/// caller a <see cref="ITelegramBotClient"/>. The actual instance is owned by
/// <see cref="TelegramBotClientCache"/> (singleton) so high-throughput periods
/// reuse one client; a token swap at runtime invalidates the cache on the next call.
/// </summary>
public class TelegramBotClientFactory {
    public const string BotTokenKey = "messaging.telegram.bot_token";
    public const string BotUsernameKey = "messaging.telegram.bot_username";
    public const string HttpClientName = "telegram";

    private readonly OptionRepository _optionRepo;
    private readonly TelegramBotClientCache _cache;

    public TelegramBotClientFactory(OptionRepository optionRepo, TelegramBotClientCache cache) {
        _optionRepo = optionRepo;
        _cache = cache;
    }

    /// <summary>Returns the cached client for the configured token, or null when no token is set.</summary>
    public virtual ITelegramBotClient? CreateOrNull() {
        var token = _optionRepo.GetGlobalValue(BotTokenKey)?.Value;
        if (string.IsNullOrWhiteSpace(token)) {
            return null;
        }
        return _cache.GetOrCreate(token);
    }

    /// <summary>Bot username (without leading @) used to construct <c>https://t.me/{username}?start={token}</c> deeplinks.</summary>
    public virtual string? GetBotUsername() {
        return _optionRepo.GetGlobalValue(BotUsernameKey)?.Value;
    }
}
