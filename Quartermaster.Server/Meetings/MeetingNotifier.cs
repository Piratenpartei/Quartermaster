using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Quartermaster.Api.Meetings;

namespace Quartermaster.Server.Meetings;

public class MeetingNotifier : IMeetingNotifier {
    private readonly IHubContext<MeetingHub> _hub;

    public MeetingNotifier(IHubContext<MeetingHub> hub) {
        _hub = hub;
    }

    public Task NotifyAgendaItemChangedAsync(Guid meetingId, Guid agendaItemId, string reason) {
        return _hub.Clients.Group(MeetingHub.GroupFor(meetingId))
            .SendAsync(MeetingHubMethods.AgendaItemChanged, new AgendaItemChangedMessage {
                MeetingId = meetingId,
                AgendaItemId = agendaItemId,
                Reason = reason
            });
    }

    public Task NotifyMeetingStatusChangedAsync(Guid meetingId, MeetingStatus newStatus) {
        return _hub.Clients.Group(MeetingHub.GroupFor(meetingId))
            .SendAsync(MeetingHubMethods.MeetingStatusChanged, new MeetingStatusChangedMessage {
                MeetingId = meetingId,
                NewStatus = newStatus
            });
    }

    public Task NotifyPresenceChangedAsync(Guid meetingId, Guid agendaItemId, Guid userId, bool isPresent) {
        return _hub.Clients.Group(MeetingHub.GroupFor(meetingId))
            .SendAsync(MeetingHubMethods.PresenceChanged, new PresenceChangedMessage {
                MeetingId = meetingId,
                AgendaItemId = agendaItemId,
                UserId = userId,
                IsPresent = isPresent
            });
    }
}
