using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using LinqToDB;
using Quartermaster.Api.Notifications;
using Quartermaster.Data;
using Quartermaster.Data.Notifications;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.Messaging;

namespace Quartermaster.Server.Notifications;

/// <summary>
/// Replaces all preference overrides for the caller. Cells that match the channel
/// default are persisted anyway — the UI sends a full matrix on save. Cells for
/// channels the caller can't receive on (e.g. email with no address on file) are
/// dropped so a later address change defaults them on rather than carrying a stale
/// opt-in/out.
/// </summary>
public class NotificationPreferencesUpdateEndpoint : Endpoint<UpdateNotificationPreferencesRequest> {
    private readonly UserNotificationPreferenceRepository _prefRepo;
    private readonly DbContext _db;
    private readonly PermissionContext _perms;

    public NotificationPreferencesUpdateEndpoint(
        UserNotificationPreferenceRepository prefRepo, DbContext db, PermissionContext perms) {
        _prefRepo = prefRepo;
        _db = db;
        _perms = perms;
    }

    public override void Configure() {
        Put("/api/users/notification-preferences");
    }

    public override async Task HandleAsync(UpdateNotificationPreferencesRequest req, CancellationToken ct) {
        var userId = _perms.UserId;
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var knownTriggerIds = NotificationTriggerCatalog.All.Select(t => t.TriggerId).ToHashSet();
        var availableChannelIds = NotificationChannelCatalog.All
            .Where(c => c.UserSelectable)
            .Select(c => c.ChannelId)
            .ToHashSet();

        var user = _db.Users
            .Where(u => u.Id == userId.Value)
            .Select(u => new { u.Email, u.TelegramChatId })
            .FirstOrDefault();
        if (user != null) {
            if (string.IsNullOrWhiteSpace(user.Email)) {
                availableChannelIds.Remove(EmailMessageChannel.ChannelId);
            }
            if (string.IsNullOrWhiteSpace(user.TelegramChatId)) {
                availableChannelIds.Remove(TelegramMessageChannel.ChannelId);
            }
        }

        var rows = req.Cells
            .Where(c => knownTriggerIds.Contains(c.TriggerId) && availableChannelIds.Contains(c.ChannelId))
            .GroupBy(c => (c.TriggerId, c.ChannelId))
            .Select(g => g.Last())
            .Select(c => new UserNotificationPreference {
                UserId = userId.Value,
                TriggerId = c.TriggerId,
                ChannelId = c.ChannelId,
                Enabled = c.Enabled
            })
            .ToList();

        _prefRepo.Replace(userId.Value, rows);
        await SendNoContentAsync(ct);
    }
}
