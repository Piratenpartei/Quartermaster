using System.Collections.Generic;
using System.Linq;
using Quartermaster.Server.AssemblyAssistant.Mumble.Proto;

namespace Quartermaster.Server.AssemblyAssistant.Mumble;

/// <summary>
/// Channel and user state of the server as seen by the bot, built from ChannelState, UserState and their remove messages.
/// UserState messages are deltas keyed by session. Not thread-safe; the connection's read loop owns it.
/// </summary>
public class MumbleServerModel {
    private readonly Dictionary<uint, ChannelData> _channels = new();
    private readonly Dictionary<uint, MumbleUser> _users = new();

    public uint? OwnSession { get; private set; }
    public bool IsSynced { get; private set; }

    public IReadOnlyList<MumbleEvent> Apply(ChannelState message) {
        if (!message.HasChannelId)
            return [];

        var channel = GetOrCreateChannel(message.ChannelId);
        if (message.HasParent)
            channel.ParentId = message.Parent;
        if (message.HasName)
            channel.Name = message.Name;

        // A ChannelState cannot express "no links" through the full list; that case comes as links_remove.
        if (message.Links.Count > 0) {
            foreach (var old in channel.Links.ToList())
                Unlink(channel.Id, old);
            foreach (var target in message.Links)
                Link(channel.Id, target);
        }
        foreach (var target in message.LinksAdd)
            Link(channel.Id, target);
        foreach (var target in message.LinksRemove)
            Unlink(channel.Id, target);

        return [];
    }

    public IReadOnlyList<MumbleEvent> Apply(ChannelRemove message) {
        if (!_channels.TryGetValue(message.ChannelId, out var channel))
            return [];

        foreach (var target in channel.Links.ToList())
            Unlink(channel.Id, target);
        _channels.Remove(message.ChannelId);
        return [];
    }

    public IReadOnlyList<MumbleEvent> Apply(UserState message) {
        if (!message.HasSession)
            return [];

        var known = _users.TryGetValue(message.Session, out var existing);
        var user = existing ?? new MumbleUser(message.Session, "", 0, null, null, false, false);

        if (message.HasName)
            user = user with { Name = message.Name };
        if (message.HasChannelId)
            user = user with { ChannelId = message.ChannelId };
        if (message.HasUserId)
            user = user with { UserId = (int)message.UserId };
        if (message.HasHash)
            user = user with { CertificateHash = string.IsNullOrEmpty(message.Hash) ? null : message.Hash };
        if (message.HasSelfMute)
            user = user with { SelfMute = message.SelfMute };
        if (message.HasSelfDeaf)
            user = user with { SelfDeaf = message.SelfDeaf };

        _users[message.Session] = user;
        if (!IsSynced)
            return [];
        if (!known)
            return [new MumbleUserEntered(user, null)];
        if (existing!.ChannelId != user.ChannelId)
            return [new MumbleUserEntered(user, existing.ChannelId)];

        return [];
    }

    public IReadOnlyList<MumbleEvent> Apply(UserRemove message) {
        if (!_users.Remove(message.Session, out var user))
            return [];

        return IsSynced ? [new MumbleUserDisconnected(user)] : [];
    }

    public IReadOnlyList<MumbleEvent> ApplyServerSync(ServerSync message) {
        if (message.HasSession)
            OwnSession = message.Session;
        IsSynced = true;
        return [new MumbleSynced(Snapshot())];
    }

    public MumbleUser? GetUser(uint session) {
        return _users.GetValueOrDefault(session);
    }

    public bool HasChannel(uint channelId) {
        return _channels.ContainsKey(channelId);
    }

    public MumbleServerSnapshot Snapshot() {
        var channels = _channels.Values
            .OrderBy(c => c.Id)
            .Select(c => new MumbleChannel(c.Id, c.ParentId, c.Name, c.Links.ToHashSet()))
            .ToList();
        var users = _users.Values
            .OrderBy(u => u.Session)
            .ToList();

        return new MumbleServerSnapshot(OwnSession, channels, users);
    }

    private ChannelData GetOrCreateChannel(uint id) {
        if (!_channels.TryGetValue(id, out var channel)) {
            channel = new ChannelData(id);
            _channels[id] = channel;
        }

        return channel;
    }

    private void Link(uint a, uint b) {
        if (a == b)
            return;

        GetOrCreateChannel(a).Links.Add(b);
        GetOrCreateChannel(b).Links.Add(a);
    }

    private void Unlink(uint a, uint b) {
        if (_channels.TryGetValue(a, out var first))
            first.Links.Remove(b);
        if (_channels.TryGetValue(b, out var second))
            second.Links.Remove(a);
    }

    private class ChannelData {
        public uint Id { get; }
        public uint? ParentId { get; set; }
        public string Name { get; set; } = "";
        public HashSet<uint> Links { get; } = [];

        public ChannelData(uint id) {
            Id = id;
        }
    }
}
