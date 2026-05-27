using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Notifications;
using Quartermaster.Data.Notifications;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Notifications;

/// <summary>
/// Returns the calling user's full notification-preference matrix: every (trigger × channel)
/// pair from the catalog, with the effective value (explicit override OR channel default).
/// </summary>
public class NotificationPreferencesGetEndpoint : EndpointWithoutRequest<NotificationPreferencesDTO> {
    private readonly UserNotificationPreferenceRepository _prefRepo;
    private readonly PermissionContext _perms;

    public NotificationPreferencesGetEndpoint(
        UserNotificationPreferenceRepository prefRepo, PermissionContext perms) {
        _prefRepo = prefRepo;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/users/notification-preferences");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        var userId = _perms.UserId;
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var overrides = _prefRepo.GetForUser(userId.Value)
            .ToDictionary(p => (p.TriggerId, p.ChannelId), p => p.Enabled);

        var cells = (
            from trigger in NotificationTriggerCatalog.All
            from channel in NotificationChannelCatalog.All
            let effective = overrides.TryGetValue((trigger.TriggerId, channel.ChannelId), out var v)
                ? v
                : NotificationDefaults.IsEnabledByDefault(channel.ChannelId)
            select new NotificationPreferenceCellDTO {
                TriggerId = trigger.TriggerId,
                ChannelId = channel.ChannelId,
                Enabled = effective
            }).ToList();

        await SendAsync(new NotificationPreferencesDTO {
            Triggers = NotificationTriggerCatalog.All
                .Select(t => new NotificationTriggerDescriptorDTO {
                    TriggerId = t.TriggerId,
                    DisplayName = t.DisplayName,
                    Description = t.Description
                }).ToList(),
            Channels = NotificationChannelCatalog.All
                .Select(c => new NotificationChannelDescriptorDTO {
                    ChannelId = c.ChannelId,
                    DisplayName = c.DisplayName,
                    Available = c.Available
                }).ToList(),
            Cells = cells
        }, cancellation: ct);
    }
}
