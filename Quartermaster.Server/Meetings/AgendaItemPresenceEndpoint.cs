using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.Extensions.Logging;
using Quartermaster.Api;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Meetings;
using Quartermaster.Data.Meetings;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Meetings;

/// <summary>
/// Toggles an officer's presence on a Presence-type agenda item.
/// Stores the set of present user IDs as JSON in the agenda item's Resolution field.
/// </summary>
public class AgendaItemPresenceEndpoint : Endpoint<AgendaItemPresenceRequest> {
    private readonly MeetingRepository _meetingRepo;
    private readonly AgendaItemRepository _agendaRepo;
    private readonly IMeetingNotifier _notifier;
    private readonly PermissionContext _perms;

    public AgendaItemPresenceEndpoint(
        MeetingRepository meetingRepo, AgendaItemRepository agendaRepo,
        IMeetingNotifier notifier, PermissionContext perms) {
        _meetingRepo = meetingRepo;
        _agendaRepo = agendaRepo;
        _notifier = notifier;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/meetings/{MeetingId}/agenda/{ItemId}/presence");
    }

    public override async Task HandleAsync(AgendaItemPresenceRequest req, CancellationToken ct) {
        var meeting = _meetingRepo.Get(req.MeetingId);
        if (meeting == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (meeting.Status != MeetingStatus.InProgress) {
            ThrowError(I18nKey.Error.Meeting.Agenda.PresenceRequiresInProgress);
            return;
        }
        var item = _agendaRepo.Get(req.ItemId);
        if (item == null || item.MeetingId != meeting.Id) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (item.ItemType != AgendaItemType.Presence) {
            ThrowError(I18nKey.Error.Meeting.Agenda.NotPresenceItem);
            return;
        }
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.Has(meeting.ChapterId, PermissionIdentifier.EditMeetings)) {
            await SendForbiddenAsync(ct);
            return;
        }

        // Load current presence list from Resolution field (JSON array of user ID strings)
        var presentIds = new HashSet<string>();
        if (!string.IsNullOrWhiteSpace(item.Resolution)) {
            try {
                var parsed = JsonSerializer.Deserialize<List<string>>(item.Resolution);
                if (parsed != null)
                    presentIds = new HashSet<string>(parsed);
            } catch (JsonException ex) {
                Logger.LogWarning(ex, "Corrupted presence Resolution on agenda item {Id}; resetting to empty", item.Id);
            }
        }

        var userIdStr = req.UserId.ToString();
        if (req.Present)
            presentIds.Add(userIdStr);
        else
            presentIds.Remove(userIdStr);

        _agendaRepo.UpdateResolution(req.ItemId, JsonSerializer.Serialize(presentIds.ToList()));
        await _notifier.NotifyPresenceChangedAsync(req.MeetingId, req.ItemId, req.UserId, req.Present);
        await SendOkAsync(ct);
    }
}
