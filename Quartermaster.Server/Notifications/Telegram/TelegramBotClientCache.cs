using System.Net.Http;
using System.Threading;
using Telegram.Bot;

namespace Quartermaster.Server.Notifications.Telegram;

/// <summary>
/// Singleton cache of one <see cref="ITelegramBotClient"/> keyed by the current
/// bot token. Reused across the long-polling receiver loop and per-request outbound
/// sends so high-burst periods (delegate assemblies etc.) don't pay the per-call
/// allocation cost. A token change invalidates and rebuilds.
/// </summary>
public class TelegramBotClientCache {
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Lock _lock = new();
    private string? _cachedToken;
    private ITelegramBotClient? _cachedClient;

    public TelegramBotClientCache(IHttpClientFactory httpClientFactory) {
        _httpClientFactory = httpClientFactory;
    }

    public ITelegramBotClient GetOrCreate(string token) {
        lock (_lock) {
            if (_cachedClient != null && _cachedToken == token) {
                return _cachedClient;
            }
            var http = _httpClientFactory.CreateClient(TelegramBotClientFactory.HttpClientName);
            _cachedClient = new TelegramBotClient(token, http);
            _cachedToken = token;
            return _cachedClient;
        }
    }
}
