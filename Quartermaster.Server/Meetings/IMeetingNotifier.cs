using System;
using System.Threading.Tasks;

namespace Quartermaster.Server.Meetings;

/// <summary>
/// Sends real-time notifications to clients connected to a meeting's SignalR
/// group. Endpoints inject this to broadcast change notifications after
/// mutations succeed.
/// </summary>
public interface IMeetingNotifier {
    Task NotifyAgendaItemChangedAsync(Guid meetingId, Guid agendaItemId, string reason);
    Task NotifyMeetingStatusChangedAsync(Guid meetingId, Quartermaster.Api.Meetings.MeetingStatus newStatus);
    Task NotifyPresenceChangedAsync(Guid meetingId, Guid agendaItemId, Guid userId, bool isPresent);
}
