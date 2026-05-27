using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Notifications;
using Quartermaster.Data;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Notifications;

public class TelegramLinkStatusEndpoint : EndpointWithoutRequest<TelegramLinkStatusDTO> {
    private readonly DbContext _db;
    private readonly PermissionContext _perms;

    public TelegramLinkStatusEndpoint(DbContext db, PermissionContext perms) {
        _db = db;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/users/telegram-link");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        var userId = _perms.UserId;
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        var chatId = _db.Users
            .Where(u => u.Id == userId.Value)
            .Select(u => u.TelegramChatId)
            .FirstOrDefault();
        await SendAsync(new TelegramLinkStatusDTO {
            Linked = !string.IsNullOrEmpty(chatId),
            ChatId = chatId
        }, cancellation: ct);
    }
}
