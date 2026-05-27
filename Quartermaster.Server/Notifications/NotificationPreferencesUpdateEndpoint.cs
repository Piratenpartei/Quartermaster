using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Notifications;
using Quartermaster.Data.Notifications;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Notifications;

/// <summary>
/// Replaces all preference overrides for the caller. Cells that match the channel
/// default are persisted anyway — the UI sends a full matrix on save.
/// </summary>
public class NotificationPreferencesUpdateEndpoint : Endpoint<UpdateNotificationPreferencesRequest> {
    private readonly UserNotificationPreferenceRepository _prefRepo;
    private readonly PermissionContext _perms;

    public NotificationPreferencesUpdateEndpoint(
        UserNotificationPreferenceRepository prefRepo, PermissionContext perms) {
        _prefRepo = prefRepo;
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
        var knownChannelIds = NotificationChannelCatalog.All.Select(c => c.ChannelId).ToHashSet();

        var rows = req.Cells
            .Where(c => knownTriggerIds.Contains(c.TriggerId) && knownChannelIds.Contains(c.ChannelId))
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
