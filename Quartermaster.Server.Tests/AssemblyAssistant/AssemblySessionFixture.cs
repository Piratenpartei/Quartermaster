using System;
using System.Collections.Generic;
using System.Linq;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Server.AssemblyAssistant.Mumble;
using Quartermaster.Server.AssemblyAssistant.OpenSlides;
using Quartermaster.Server.AssemblyAssistant.Session;

namespace Quartermaster.Server.Tests.AssemblyAssistant;

/// <summary>Drives an <see cref="AssemblySessionState"/> like the Mumble connection would, with an explicit clock.</summary>
public class AssemblySessionFixture {
    public const uint Chair = 1;
    public const uint Speaker = 2;
    public const uint MotionAuthor = 3;
    public const uint Pro = 4;
    public const uint Contra = 5;
    public const uint Question = 6;
    public const uint Point = 7;
    public const uint Listener = 8;
    public const uint Lobby = 20;
    public const uint BotSession = 100;

    public static readonly Guid Actor = Guid.NewGuid();

    public static readonly IReadOnlyDictionary<ChannelRole, uint> Channels = new Dictionary<ChannelRole, uint> {
        [ChannelRole.Chair] = Chair,
        [ChannelRole.Speaker] = Speaker,
        [ChannelRole.MotionAuthor] = MotionAuthor,
        [ChannelRole.Pro] = Pro,
        [ChannelRole.Contra] = Contra,
        [ChannelRole.Question] = Question,
        [ChannelRole.Point] = Point,
        [ChannelRole.Listener] = Listener
    };

    private readonly Dictionary<uint, MumbleUser> _users = new();

    public AssemblySessionState State { get; }
    public DateTime Now { get; set; } = new(2026, 11, 28, 10, 0, 0, DateTimeKind.Utc);

    public AssemblySessionFixture(AssemblySessionOptions? options = null, AssemblyPollState? polls = null) {
        State = new AssemblySessionState(Guid.NewGuid(), Channels, options ?? AssemblySessionOptions.Default, polls);
    }

    public MumbleUser User(uint session, string name, uint channel = Listener, int? userId = null, string? hash = null) {
        var user = new MumbleUser(session, name, channel, userId, hash, false, false);
        _users[session] = user;
        return user;
    }

    public AssemblyEffects Sync() {
        var bot = new MumbleUser(BotSession, "Versammlungsassistent", Chair, 1, "bot", true, false);
        var snapshot = new MumbleServerSnapshot(BotSession, [], _users.Values.Append(bot).ToList());
        return State.OnSynced(snapshot, Now);
    }

    public AssemblyEffects Move(uint session, uint channel) {
        var previous = _users[session];
        var moved = previous with { ChannelId = channel };
        _users[session] = moved;
        return State.OnUserEntered(moved, previous.ChannelId, Now);
    }

    public AssemblyEffects Connect(MumbleUser user) {
        _users[user.Session] = user;
        return State.OnUserEntered(user, null, Now);
    }

    public AssemblyEffects Disconnect(uint session) {
        var user = _users[session];
        _users.Remove(session);
        return State.OnUserDisconnected(user, Now);
    }

    public void Advance(TimeSpan by) {
        Now += by;
    }

    public AssemblyQueueDTO Queue(QueueKind kind) {
        return State.Snapshot(Now).Queues.Single(q => q.Kind == kind);
    }

    public List<string> Names(QueueKind kind) {
        return Queue(kind).Entries.Select(e => e.Name).ToList();
    }

    public AssemblyQueueEntryDTO Entry(QueueKind kind, string name) {
        return Queue(kind).Entries.Single(e => e.Name == name);
    }

    public AssemblyFloorDTO? FloorOf(uint session) {
        return State.Snapshot(Now).Floor.SingleOrDefault(f => f.Session == session);
    }
}
