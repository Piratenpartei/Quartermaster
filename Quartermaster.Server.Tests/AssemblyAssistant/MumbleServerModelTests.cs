using System.Linq;
using System.Threading.Tasks;
using Quartermaster.Server.AssemblyAssistant.Mumble;
using Quartermaster.Server.AssemblyAssistant.Mumble.Proto;

namespace Quartermaster.Server.Tests.AssemblyAssistant;

public class MumbleServerModelTests {
    private static MumbleServerModel SyncedModel() {
        var model = new MumbleServerModel();
        model.Apply(new ChannelState { ChannelId = 0, Name = "Root" });
        model.Apply(new ChannelState { ChannelId = 1, Parent = 0, Name = "Zuhörer" });
        model.Apply(new ChannelState { ChannelId = 2, Parent = 0, Name = "Redner" });
        model.Apply(new UserState { Session = 10, Name = "Bot", ChannelId = 1 });
        model.ApplyServerSync(new ServerSync { Session = 10 });
        return model;
    }

    [Test]
    public async Task Initial_user_without_channel_is_in_root() {
        var model = new MumbleServerModel();
        model.Apply(new UserState { Session = 5, Name = "Pirat" });

        var events = model.ApplyServerSync(new ServerSync { Session = 9 });

        var synced = (MumbleSynced)events.Single();
        await Assert.That(synced.Snapshot.OwnSession).IsEqualTo(9u);
        await Assert.That(synced.Snapshot.Users.Single().ChannelId).IsEqualTo(0u);
    }

    [Test]
    public async Task No_entered_events_before_sync() {
        var model = new MumbleServerModel();

        var events = model.Apply(new UserState { Session = 5, Name = "Pirat", ChannelId = 2 });

        await Assert.That(events).IsEmpty();
    }

    [Test]
    public async Task UserState_delta_keeps_unset_fields() {
        var model = SyncedModel();
        model.Apply(new UserState { Session = 5, Name = "Pirat", ChannelId = 1, UserId = 77, Hash = "abc", SelfMute = true });

        model.Apply(new UserState { Session = 5, SelfDeaf = true });

        var user = model.GetUser(5)!;
        await Assert.That(user.Name).IsEqualTo("Pirat");
        await Assert.That(user.ChannelId).IsEqualTo(1u);
        await Assert.That(user.UserId).IsEqualTo(77);
        await Assert.That(user.SelfMute).IsTrue();
        await Assert.That(user.SelfDeaf).IsTrue();
    }

    [Test]
    public async Task New_user_after_sync_raises_entered_without_previous() {
        var model = SyncedModel();

        var events = model.Apply(new UserState { Session = 5, Name = "Pirat", ChannelId = 1 });

        var entered = (MumbleUserEntered)events.Single();
        await Assert.That(entered.User.Session).IsEqualTo(5u);
        await Assert.That(entered.PreviousChannelId).IsNull();
    }

    [Test]
    public async Task Move_raises_entered_with_previous_channel() {
        var model = SyncedModel();
        model.Apply(new UserState { Session = 5, Name = "Pirat", ChannelId = 1 });

        var events = model.Apply(new UserState { Session = 5, ChannelId = 2 });

        var entered = (MumbleUserEntered)events.Single();
        await Assert.That(entered.User.ChannelId).IsEqualTo(2u);
        await Assert.That(entered.PreviousChannelId).IsEqualTo(1u);
    }

    [Test]
    public async Task State_change_without_move_raises_nothing() {
        var model = SyncedModel();
        model.Apply(new UserState { Session = 5, Name = "Pirat", ChannelId = 1 });

        var events = model.Apply(new UserState { Session = 5, SelfMute = true });

        await Assert.That(events).IsEmpty();
    }

    [Test]
    public async Task UserRemove_raises_disconnected() {
        var model = SyncedModel();
        model.Apply(new UserState { Session = 5, Name = "Pirat", ChannelId = 1 });

        var events = model.Apply(new UserRemove { Session = 5 });

        var disconnected = (MumbleUserDisconnected)events.Single();
        await Assert.That(disconnected.User.Name).IsEqualTo("Pirat");
        await Assert.That(model.GetUser(5)).IsNull();
    }

    [Test]
    public async Task Links_add_and_remove_are_symmetric() {
        var model = SyncedModel();
        var add = new ChannelState { ChannelId = 1 };
        add.LinksAdd.Add(2);
        model.Apply(add);

        var linked = model.Snapshot().Channels.Single(c => c.Id == 2);
        await Assert.That(linked.Links).Contains(1u);

        var remove = new ChannelState { ChannelId = 2 };
        remove.LinksRemove.Add(1);
        model.Apply(remove);

        var unlinked = model.Snapshot().Channels.Single(c => c.Id == 1);
        await Assert.That(unlinked.Links).IsEmpty();
    }

    [Test]
    public async Task Full_links_list_replaces_previous_links() {
        var model = SyncedModel();
        model.Apply(new ChannelState { ChannelId = 3, Parent = 0, Name = "Contra" });
        var first = new ChannelState { ChannelId = 1 };
        first.Links.Add(2);
        model.Apply(first);

        var second = new ChannelState { ChannelId = 1 };
        second.Links.Add(3);
        model.Apply(second);

        var snapshot = model.Snapshot();
        await Assert.That(snapshot.Channels.Single(c => c.Id == 1).Links).IsEquivalentTo([3u]);
        await Assert.That(snapshot.Channels.Single(c => c.Id == 2).Links).IsEmpty();
    }

    [Test]
    public async Task Hash_is_kept() {
        var model = SyncedModel();
        model.Apply(new UserState { Session = 5, Name = "Pirat", ChannelId = 1, Hash = "deadbeef" });

        await Assert.That(model.GetUser(5)!.CertificateHash).IsEqualTo("deadbeef");
    }
}
