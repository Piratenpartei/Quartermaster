using System;
using Quartermaster.Api.AssemblyAssistant;

namespace Quartermaster.Server.AssemblyAssistant.Session;

/// <summary>A member in the speaker or motion author channel.</summary>
public class FloorHolder {
    public uint Session { get; set; }
    public MemberIdentity Identity { get; set; }
    public ChannelRole Channel { get; }
    public SpeakerRole Role { get; }
    public DateTime ArrivedAt { get; }
    public SpeakingTimer Timer { get; }
    public Guid LogEntryId { get; } = Guid.NewGuid();
    public DateTime? DoneAt { get; set; }

    public FloorHolder(uint session, MemberIdentity identity, ChannelRole channel, SpeakerRole role, DateTime arrivedAt, SpeakingTimer timer) {
        Session = session;
        Identity = identity;
        Channel = channel;
        Role = role;
        ArrivedAt = arrivedAt;
        Timer = timer;
    }

    public AssemblyFloorDTO ToDto(DateTime nowUtc, TimeSpan startNowAfter) {
        var offerStartNow = !Timer.IsStarted && DoneAt == null && nowUtc - ArrivedAt >= startNowAfter;
        return new AssemblyFloorDTO {
            Session = Session,
            Name = Identity.Name,
            Channel = Channel,
            Role = Role,
            ArrivedAt = ArrivedAt,
            DonePending = DoneAt != null,
            Timer = Timer.ToDto(nowUtc, offerStartNow)
        };
    }
}
