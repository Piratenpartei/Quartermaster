using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Notifications;
using Quartermaster.Data.Notifications;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.Notifications.Telegram;

namespace Quartermaster.Server.Notifications;

/// <summary>
/// Generates a fresh short-lived link token for the caller. The returned deeplink
/// is null when the bot username isn't configured — the client should then show
/// the raw token and let the user paste it manually.
/// </summary>
public class TelegramLinkStartEndpoint : EndpointWithoutRequest<TelegramLinkStartDTO> {
    private readonly TelegramLinkTokenRepository _tokenRepo;
    private readonly TelegramBotClientFactory _factory;
    private readonly PermissionContext _perms;

    public TelegramLinkStartEndpoint(
        TelegramLinkTokenRepository tokenRepo,
        TelegramBotClientFactory factory,
        PermissionContext perms) {
        _tokenRepo = tokenRepo;
        _factory = factory;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/users/telegram-link");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        var userId = _perms.UserId;
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        var token = _tokenRepo.Create(userId.Value, DateTime.UtcNow);
        var botUsername = _factory.GetBotUsername();
        var deeplink = string.IsNullOrEmpty(botUsername)
            ? null
            : $"https://t.me/{botUsername}";
        await SendAsync(new TelegramLinkStartDTO {
            Token = token.Token,
            ExpiresAt = token.ExpiresAt,
            Deeplink = deeplink,
            BotUsername = botUsername
        }, cancellation: ct);
    }
}
