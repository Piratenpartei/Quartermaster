using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.Messaging;

/// <summary>
/// Telegram Bot API outbound. <see cref="ChannelMessage.ChannelAddress"/> is the chat id
/// (numeric for DMs, <c>@channelname</c> for public channels). Sync HTTP per message.
/// <para>
/// V1 outbound-only via raw HTTP — the production path (Telegram.Bot package + hosted
/// long-polling receiver for chat-id discovery via <c>/start</c>) is tracked in the
/// notification system feature todo.
/// </para>
/// </summary>
public class TelegramMessageChannel : IMessageChannel {
    public const string ChannelId = "telegram";
    private const string ApiBaseUrl = "https://api.telegram.org";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OptionRepository _optionRepo;
    private readonly ILogger<TelegramMessageChannel> _logger;

    public TelegramMessageChannel(
        IHttpClientFactory httpClientFactory,
        OptionRepository optionRepo,
        ILogger<TelegramMessageChannel> logger) {
        _httpClientFactory = httpClientFactory;
        _optionRepo = optionRepo;
        _logger = logger;
    }

    public string Id => ChannelId;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(GetBotToken());

    public async Task<ChannelDeliveryResult> SendAsync(ChannelMessage message, CancellationToken ct = default) {
        var token = GetBotToken();
        if (string.IsNullOrWhiteSpace(token))
            return ChannelDeliveryResult.Fail("Telegram bot token is not configured.");

        if (string.IsNullOrWhiteSpace(message.ChannelAddress))
            return ChannelDeliveryResult.Fail("Telegram chat id is empty.");

        var client = _httpClientFactory.CreateClient(ChannelId);
        var url = $"{ApiBaseUrl}/bot{token}/sendMessage";
        var text = string.IsNullOrEmpty(message.Subject)
            ? message.Body
            : $"*{message.Subject}*\n\n{message.Body}";

        HttpResponseMessage response;
        try {
            response = await client.PostAsJsonAsync(url, new {
                chat_id = message.ChannelAddress,
                text,
                parse_mode = "Markdown"
            }, ct);
        } catch (HttpRequestException ex) {
            _logger.LogWarning(ex, "Telegram HTTP request failed for chat {ChatId}", message.ChannelAddress);
            return ChannelDeliveryResult.Fail($"Telegram request failed: {ex.Message}");
        }

        if (!response.IsSuccessStatusCode) {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Telegram returned {Status} for chat {ChatId}: {Body}",
                response.StatusCode, message.ChannelAddress, body);
            return ChannelDeliveryResult.Fail($"Telegram returned {(int)response.StatusCode}: {body}");
        }

        // Bot API returns 200 with {ok:false,...} for application-level errors (bad chat id, blocked bot, etc.).
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        if (payload.TryGetProperty("ok", out var ok) && !ok.GetBoolean()) {
            var description = payload.TryGetProperty("description", out var d) ? d.GetString() : "(no description)";
            _logger.LogWarning("Telegram API rejected message for chat {ChatId}: {Description}",
                message.ChannelAddress, description);
            return ChannelDeliveryResult.Fail($"Telegram rejected message: {description}");
        }

        return ChannelDeliveryResult.Ok();
    }

    private string? GetBotToken() => _optionRepo.GetGlobalValue("messaging.telegram.bot_token")?.Value;
}
