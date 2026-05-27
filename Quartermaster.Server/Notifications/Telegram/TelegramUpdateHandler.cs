using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartermaster.Data.Notifications;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Quartermaster.Server.Notifications.Telegram;

/// <summary>
/// Processes a single Telegram <see cref="Update"/>. Pulled out of the background
/// service so it can be unit-tested with synthetic updates without talking to the
/// real Bot API.
/// </summary>
public class TelegramUpdateHandler {
    private const string StartCommand = "/start";
    private const string LinkCommand = "/link";

    private readonly TelegramLinkTokenRepository _tokenRepo;
    private readonly ILogger<TelegramUpdateHandler> _logger;

    public TelegramUpdateHandler(TelegramLinkTokenRepository tokenRepo, ILogger<TelegramUpdateHandler> logger) {
        _tokenRepo = tokenRepo;
        _logger = logger;
    }

    public async Task HandleAsync(ITelegramBotClient bot, Update update, DateTime now, CancellationToken ct) {
        if (update.Message?.Text == null) {
            return;
        }
        var text = update.Message.Text.Trim();
        var chatId = update.Message.Chat.Id;

        if (text.StartsWith(LinkCommand, StringComparison.Ordinal)) {
            await HandleLinkAsync(bot, chatId, text, now, ct);
            return;
        }
        if (text.StartsWith(StartCommand, StringComparison.Ordinal)) {
            await ReplyAsync(bot, chatId,
                "Willkommen bei Quartermaster! Um deinen Telegram-Account zu verknüpfen, sende /link <Token>. Den Token findest du in deinem Quartermaster-Account unter Benachrichtigungen.",
                ct);
            return;
        }
        await ReplyAsync(bot, chatId,
            "Hallo! Benutze /link <Token>, um deinen Telegram-Account mit Quartermaster zu verknüpfen. Den Token findest du in deinem Account unter Benachrichtigungen.",
            ct);
    }

    private async Task HandleLinkAsync(ITelegramBotClient bot, long chatId, string text, DateTime now, CancellationToken ct) {
        var token = text.Length > LinkCommand.Length
            ? text.Substring(LinkCommand.Length).Trim()
            : "";
        if (string.IsNullOrEmpty(token)) {
            await ReplyAsync(bot, chatId,
                "Bitte einen Link-Token mitschicken: /link <Token>. Den findest du in deinem Quartermaster-Account unter Benachrichtigungen.",
                ct);
            return;
        }

        var userId = _tokenRepo.Consume(token, chatId.ToString(CultureInfo.InvariantCulture), now);
        if (userId == null) {
            _logger.LogInformation("Telegram link token rejected (unknown, expired, or already consumed) chat={ChatId}", chatId);
            await ReplyAsync(bot, chatId,
                "Dieser Link-Token ist ungültig, abgelaufen oder wurde bereits benutzt. Bitte erstelle in deinem Quartermaster-Account einen neuen Link.",
                ct);
            return;
        }

        _logger.LogInformation("Telegram chat {ChatId} linked to user {UserId}", chatId, userId);
        await ReplyAsync(bot, chatId,
            "✅ Verknüpfung erfolgreich. Du erhältst ab sofort die ausgewählten Benachrichtigungen hier in Telegram.",
            ct);
    }

    private static Task ReplyAsync(ITelegramBotClient bot, long chatId, string text, CancellationToken ct) {
        return bot.SendMessage(new ChatId(chatId), text, parseMode: ParseMode.None, cancellationToken: ct);
    }
}
