using System;
using System.Collections.Generic;

namespace Quartermaster.Server.AssemblyAssistant.Mumble;

public enum MumbleConnectionState {
    Disconnected = 0,
    Connecting = 1,
    Synced = 2,
    Reconnecting = 3
}

public record MumbleChannel(uint Id, uint? ParentId, string Name, IReadOnlySet<uint> Links);

public record MumbleUser(uint Session, string Name, uint ChannelId, int? UserId, string? CertificateHash, bool SelfMute, bool SelfDeaf);

public record MumbleServerSnapshot(uint? OwnSession, IReadOnlyList<MumbleChannel> Channels, IReadOnlyList<MumbleUser> Users) {
    public static MumbleServerSnapshot Empty { get; } = new(null, [], []);
}

public abstract record MumbleEvent;

/// <summary>Raised when a user changes channel after the initial sync, and for users who connect after it.</summary>
public record MumbleUserEntered(MumbleUser User, uint? PreviousChannelId) : MumbleEvent;

public record MumbleUserDisconnected(MumbleUser User) : MumbleEvent;

public record MumbleSpeakingStarted(uint Session, DateTime AtUtc) : MumbleEvent;

public record MumbleSpeakingStopped(uint Session, DateTime AtUtc) : MumbleEvent;

public record MumblePermissionDenied(uint? Session, uint? ChannelId, string Reason) : MumbleEvent;

public record MumbleConnectionChanged(MumbleConnectionState State, string? Error) : MumbleEvent;

/// <summary>Initial sync complete; the snapshot is the full server state.</summary>
public record MumbleSynced(MumbleServerSnapshot Snapshot) : MumbleEvent;

public record MumbleSelfMovedBack(uint FromChannelId) : MumbleEvent;
