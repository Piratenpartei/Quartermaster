using System;

namespace Quartermaster.Api.Meetings;

public class AgendaItemPresenceRequest {
    public Guid MeetingId { get; set; }
    public Guid ItemId { get; set; }
    public Guid UserId { get; set; }
    public bool Present { get; set; }
}
