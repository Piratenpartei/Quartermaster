using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartermaster.Data.Notifications;
using Quartermaster.Server.Notifications.Telegram;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Quartermaster.Server.Messaging;

/// <summary>
/// Outbound Telegram delivery via <see cref="ITelegramBotClient.SendMessage"/>.
/// Synchronous send — writes a Pending <see cref="NotificationLog"/> row and updates
/// to Sent/Failed inline. <see cref="ChannelMessage.ChannelAddress"/> is the chat id
/// (numeric for DMs, <c>@channelname</c> for public channels).
/// </summary>
public class TelegramMessageChannel : IMessageChannel {
    public const string ChannelId = "telegram";

    /// <summary>Waits before each retry of a transient failure; its length is the number of retries.</summary>
    public IReadOnlyList<TimeSpan> RetryDelays { get; init; } = [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)];

    private readonly TelegramBotClientFactory _factory;
    private readonly NotificationLogRepository _logRepo;
    private readonly ILogger<TelegramMessageChannel> _logger;

    public TelegramMessageChannel(
        TelegramBotClientFactory factory,
        NotificationLogRepository logRepo,
        ILogger<TelegramMessageChannel> logger) {
        _factory = factory;
        _logRepo = logRepo;
        _logger = logger;
    }

    public string Id => ChannelId;

    public bool IsConfigured => _factory.CreateOrNull() != null;

    public NotificationBodyFormat BodyFormat => NotificationBodyFormat.Text;

    public async Task<ChannelDeliveryResult> SendAsync(ChannelMessage message, CancellationToken ct = default) {
        var bot = _factory.CreateOrNull();
        if (bot == null) {
            return ChannelDeliveryResult.Fail("Telegram bot token is not configured.");
        }
        if (string.IsNullOrWhiteSpace(message.ChannelAddress)) {
            return ChannelDeliveryResult.Fail("Telegram chat id is empty.");
        }

        var log = WritePendingLog(message);

        var text = string.IsNullOrEmpty(message.Subject)
            ? message.Body
            : $"*{message.Subject}*\n\n{message.Body}";
        var chatId = ParseChatId(message.ChannelAddress);

        for (var attempt = 0; ; attempt++) {
            try {
                await bot.SendMessage(chatId, text, parseMode: ParseMode.Markdown, cancellationToken: ct);
                _logRepo.IncrementAttempt(log.Id);
                _logRepo.UpdateStatus(log.Id, "Sent", null, DateTime.UtcNow);
                return ChannelDeliveryResult.Ok();
            } catch (Exception ex) {
                _logRepo.IncrementAttempt(log.Id);
                if (attempt < RetryDelays.Count && IsTransient(ex, ct)) {
                    _logger.LogWarning(ex, "Telegram send to chat {ChatId} failed, retrying in {Delay}", message.ChannelAddress, RetryDelays[attempt]);
                    await Task.Delay(RetryDelays[attempt], ct);
                    continue;
                }

                _logger.LogWarning(ex, "Telegram send failed for chat {ChatId}", message.ChannelAddress);
                _logRepo.UpdateStatus(log.Id, "Failed", ex.Message, null);
                return ChannelDeliveryResult.Fail($"Telegram send failed: {ex.Message}");
            }
        }
    }

    /// <summary>Telegram.Bot already waits out a short 429 by itself, so only timeouts, connection failures and 5xx get another attempt.</summary>
    private static bool IsTransient(Exception ex, CancellationToken ct) {
        if (ct.IsCancellationRequested)
            return false;
        if (ex is ApiRequestException apiError)
            return apiError.ErrorCode >= 500;
        if (ex is RequestException)
            return true;
        return ex is HttpRequestException;
    }

    private NotificationLog WritePendingLog(ChannelMessage message) {
        var meta = message.Metadata;
        var templateIdentifier = TryGet(meta, NotificationLogMetadataKeys.TemplateIdentifier);
        var triggerId = TryGet(meta, NotificationLogMetadataKeys.TriggerId);
        Guid? recipientUserId = null;
        var recipientUserIdStr = TryGet(meta, NotificationLogMetadataKeys.RecipientUserId);
        if (recipientUserIdStr != null && Guid.TryParse(recipientUserIdStr, out var parsedUser)) {
            recipientUserId = parsedUser;
        }
        Guid? recipientMemberId = null;
        var recipientMemberIdStr = TryGet(meta, NotificationLogMetadataKeys.RecipientMemberId);
        if (recipientMemberIdStr != null && Guid.TryParse(recipientMemberIdStr, out var parsedMember)) {
            recipientMemberId = parsedMember;
        }
        var log = new NotificationLog {
            ChannelId = ChannelId,
            Recipient = message.ChannelAddress,
            RecipientUserId = recipientUserId,
            RecipientMemberId = recipientMemberId,
            Subject = message.Subject,
            TriggerId = triggerId,
            TemplateIdentifier = templateIdentifier,
            SourceEntityType = message.SourceEntityType,
            SourceEntityId = message.SourceEntityId,
            Status = "Pending",
            AttemptCount = 0,
            CreatedAt = DateTime.UtcNow,
            Body = message.Body
        };
        _logRepo.Create(log);
        return log;
    }

    private static string? TryGet(IReadOnlyDictionary<string, string>? meta, string key) {
        return meta != null && meta.TryGetValue(key, out var v) ? v : null;
    }

    private static ChatId ParseChatId(string raw) {
        if (long.TryParse(raw, out var numeric)) {
            return new ChatId(numeric);
        }
        return new ChatId(raw);
    }
}
