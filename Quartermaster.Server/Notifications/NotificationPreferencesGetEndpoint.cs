using System.Collections.Generic;
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
/// Returns the calling user's full notification-preference matrix: every (trigger × channel)
/// pair from the user-selectable catalog, with the effective value (explicit override OR
/// channel default). Channels the caller can't actually receive on (e.g. email with no
/// address on file) come back with <c>Available = false</c> and an unselected cell.
/// </summary>
public class NotificationPreferencesGetEndpoint : EndpointWithoutRequest<NotificationPreferencesDTO> {
    private readonly UserNotificationPreferenceRepository _prefRepo;
    private readonly DbContext _db;
    private readonly PermissionContext _perms;

    public NotificationPreferencesGetEndpoint(
        UserNotificationPreferenceRepository prefRepo, DbContext db, PermissionContext perms) {
        _prefRepo = prefRepo;
        _db = db;
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

        var userChannels = NotificationChannelCatalog.All.Where(c => c.UserSelectable).ToList();
        var unavailableReason = BuildUnavailableReasons(userId.Value, userChannels);

        var cells = NotificationTriggerCatalog.All
            .SelectMany(_ => userChannels, (t, c) => new NotificationPreferenceCellDTO {
                TriggerId = t.TriggerId,
                ChannelId = c.ChannelId,
                Enabled = unavailableReason.ContainsKey(c.ChannelId)
                    ? false
                    : overrides.TryGetValue((t.TriggerId, c.ChannelId), out var v)
                        ? v
                        : NotificationDefaults.IsEnabledByDefault(c.ChannelId)
            })
            .ToList();

        await SendAsync(new NotificationPreferencesDTO {
            Triggers = NotificationTriggerCatalog.All
                .Select(t => new NotificationTriggerDescriptorDTO {
                    TriggerId = t.TriggerId,
                    DisplayName = t.DisplayName,
                    Description = t.Description
                }).ToList(),
            Channels = userChannels
                .Select(c => new NotificationChannelDescriptorDTO {
                    ChannelId = c.ChannelId,
                    DisplayName = c.DisplayName,
                    Available = c.Available && !unavailableReason.ContainsKey(c.ChannelId),
                    UnavailableReason = unavailableReason.TryGetValue(c.ChannelId, out var r) ? r : null
                }).ToList(),
            Cells = cells
        }, cancellation: ct);
    }

    private Dictionary<string, string> BuildUnavailableReasons(System.Guid userId, List<NotificationChannelDescriptor> userChannels) {
        var reasons = new Dictionary<string, string>();
        var needsEmail = userChannels.Any(c => c.ChannelId == EmailMessageChannel.ChannelId);
        var needsTelegram = userChannels.Any(c => c.ChannelId == TelegramMessageChannel.ChannelId);
        if (!needsEmail && !needsTelegram) {
            return reasons;
        }
        var user = _db.Users
            .Where(u => u.Id == userId)
            .Select(u => new { u.Email, u.TelegramChatId })
            .FirstOrDefault();
        if (user == null) {
            return reasons;
        }
        if (needsEmail && string.IsNullOrWhiteSpace(user.Email)) {
            reasons[EmailMessageChannel.ChannelId] = "Keine E-Mail-Adresse hinterlegt";
        }
        if (needsTelegram && string.IsNullOrWhiteSpace(user.TelegramChatId)) {
            reasons[TelegramMessageChannel.ChannelId] = "Telegram-Konto nicht verknüpft";
        }
        return reasons;
    }
}
