using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Data.Notifications;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Notifications;

/// <summary>Clears the caller's Telegram chat id and revokes any unconsumed link tokens.</summary>
public class TelegramLinkUnlinkEndpoint : EndpointWithoutRequest {
    private readonly TelegramLinkTokenRepository _tokenRepo;
    private readonly PermissionContext _perms;

    public TelegramLinkUnlinkEndpoint(TelegramLinkTokenRepository tokenRepo, PermissionContext perms) {
        _tokenRepo = tokenRepo;
        _perms = perms;
    }

    public override void Configure() {
        Delete("/api/users/telegram-link");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        var userId = _perms.UserId;
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        _tokenRepo.Unlink(userId.Value);
        await SendNoContentAsync(ct);
    }
}
