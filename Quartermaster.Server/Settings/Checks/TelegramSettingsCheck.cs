using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Settings;
using Quartermaster.Server.Notifications.Telegram;
using Telegram.Bot;
using Telegram.Bot.Exceptions;

namespace Quartermaster.Server.Settings.Checks;

/// <summary>Asks Telegram who the bot is; the returned username fills <c>messaging.telegram.bot_username</c> in the assistant.</summary>
public class TelegramSettingsCheck : ISettingsCheck {
    private readonly TelegramBotClientFactory _factory;
    private readonly ILogger<TelegramSettingsCheck> _logger;

    public TelegramSettingsCheck(TelegramBotClientFactory factory, ILogger<TelegramSettingsCheck> logger) {
        _factory = factory;
        _logger = logger;
    }

    public SettingsCheckKind Kind => SettingsCheckKind.Telegram;

    public async Task<SettingsCheckResultDTO> RunAsync(CancellationToken ct) {
        var client = _factory.CreateOrNull();
        if (client == null)
            return SettingsCheckResults.NotConfigured();

        try {
            var bot = await client.GetMe(ct);
            var username = bot.Username ?? "";
            return SettingsCheckResults.Ok(I18nParams.With(I18nKey.Ui.SettingsCheck.TelegramOk, ("username", username)), [username]);
        } catch (RequestException ex) {
            _logger.LogWarning("Telegram settings check failed: {Exception}", ex);
            return SettingsCheckResults.Failed(I18nKey.Ui.SettingsCheck.Failed, ex.Message);
        } catch (Exception ex) when (!ct.IsCancellationRequested) {
            _logger.LogWarning("Telegram settings check failed: {Exception}", ex);
            return SettingsCheckResults.Failed(I18nKey.Ui.SettingsCheck.Failed, SettingsCheckResults.UnexpectedErrorDetail);
        }
    }
}
