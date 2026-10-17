using System;
using System.Linq;
using System.Threading.Tasks;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Api.I18n;
using Quartermaster.Server.AssemblyAssistant.Mumble;
using Quartermaster.Server.AssemblyAssistant.Session;

namespace Quartermaster.Server.Tests.AssemblyAssistant;

public class AssemblySessionStateQueueTests {
    [Test]
    public async Task Entering_request_channel_appends_in_order() {
        var fixture = new AssemblySessionFixture();
        fixture.User(10, "Anna");
        fixture.User(11, "Bert");
        fixture.Sync();

        fixture.Move(11, AssemblySessionFixture.Pro);
        fixture.Advance(TimeSpan.FromSeconds(1));
        fixture.Move(10, AssemblySessionFixture.Pro);

        await Assert.That(fixture.Names(QueueKind.Pro)).IsEquivalentTo(["Bert", "Anna"]);
    }

    [Test]
    public async Task Leaving_removes_entry() {
        var fixture = new AssemblySessionFixture();
        fixture.User(10, "Anna");
        fixture.Sync();
        fixture.Move(10, AssemblySessionFixture.Contra);

        fixture.Move(10, AssemblySessionFixture.Listener);

        await Assert.That(fixture.Names(QueueKind.Contra)).IsEmpty();
    }

    [Test]
    public async Task Rejoin_within_grace_keeps_position() {
        var fixture = new AssemblySessionFixture();
        fixture.User(10, "Anna", hash: "a");
        fixture.User(11, "Bert", hash: "b");
        fixture.Sync();
        fixture.Move(10, AssemblySessionFixture.Contra);
        fixture.Advance(TimeSpan.FromSeconds(1));
        fixture.Move(11, AssemblySessionFixture.Contra);

        fixture.Disconnect(10);
        fixture.Advance(TimeSpan.FromSeconds(20));
        fixture.Connect(new MumbleUser(12, "Anna", AssemblySessionFixture.Contra, null, "a", false, false));

        await Assert.That(fixture.Names(QueueKind.Contra)).IsEquivalentTo(["Anna", "Bert"]);
        await Assert.That(fixture.Entry(QueueKind.Contra, "Anna").Session).IsEqualTo(12u);
    }

    [Test]
    public async Task Rejoin_after_grace_appends() {
        var fixture = new AssemblySessionFixture();
        fixture.User(10, "Anna", hash: "a");
        fixture.User(11, "Bert", hash: "b");
        fixture.Sync();
        fixture.Move(10, AssemblySessionFixture.Contra);
        fixture.Advance(TimeSpan.FromSeconds(1));
        fixture.Move(11, AssemblySessionFixture.Contra);

        fixture.Move(10, AssemblySessionFixture.Listener);
        fixture.Advance(TimeSpan.FromSeconds(31));
        fixture.State.Tick(fixture.Now);
        fixture.Move(10, AssemblySessionFixture.Contra);

        await Assert.That(fixture.Names(QueueKind.Contra)).IsEquivalentTo(["Bert", "Anna"]);
    }

    [Test]
    public async Task Rejoin_with_new_session_matches_by_hash() {
        var fixture = new AssemblySessionFixture();
        fixture.User(10, "Anna", hash: "a");
        fixture.User(11, "Bert", hash: "b");
        fixture.Sync();
        fixture.Move(10, AssemblySessionFixture.Pro);
        fixture.Advance(TimeSpan.FromSeconds(1));
        fixture.Move(11, AssemblySessionFixture.Pro);

        fixture.Disconnect(10);
        fixture.Connect(new MumbleUser(30, "Anna (Handy)", AssemblySessionFixture.Pro, null, "a", false, false));

        await Assert.That(fixture.Names(QueueKind.Pro)).IsEquivalentTo(["Anna (Handy)", "Bert"]);
    }

    [Test]
    public async Task Registered_user_matches_by_user_id() {
        var fixture = new AssemblySessionFixture();
        fixture.User(10, "Anna", userId: 7, hash: "old");
        fixture.User(11, "Bert");
        fixture.Sync();
        fixture.Move(10, AssemblySessionFixture.Pro);
        fixture.Advance(TimeSpan.FromSeconds(1));
        fixture.Move(11, AssemblySessionFixture.Pro);

        fixture.Disconnect(10);
        fixture.Connect(new MumbleUser(31, "Anna", AssemblySessionFixture.Pro, 7, "new-certificate", false, false));

        await Assert.That(fixture.Names(QueueKind.Pro)).IsEquivalentTo(["Anna", "Bert"]);
    }

    [Test]
    public async Task Late_entry_after_close_is_flagged_and_not_suggested() {
        var fixture = new AssemblySessionFixture();
        fixture.User(10, "Anna");
        fixture.User(11, "Bert");
        fixture.Sync();
        fixture.Move(10, AssemblySessionFixture.Pro);
        fixture.State.CloseList(fixture.State.Version, null, AssemblySessionFixture.Actor, fixture.Now);
        fixture.Advance(TimeSpan.FromSeconds(1));

        fixture.Move(11, AssemblySessionFixture.Pro);

        await Assert.That(fixture.Entry(QueueKind.Pro, "Anna").AfterListClosed).IsFalse();
        await Assert.That(fixture.Entry(QueueKind.Pro, "Bert").AfterListClosed).IsTrue();
        await Assert.That(fixture.State.Suggestion()!.Identity.Name).IsEqualTo("Anna");

        fixture.State.GiveFloor(fixture.State.Version, fixture.Entry(QueueKind.Pro, "Anna").Key, AssemblySessionFixture.Actor, fixture.Now);
        fixture.Move(10, AssemblySessionFixture.Speaker);
        await Assert.That(fixture.State.Suggestion()).IsNull();
    }

    [Test]
    public async Task Close_list_at_time_takes_effect_on_tick() {
        var fixture = new AssemblySessionFixture();
        fixture.User(10, "Anna");
        fixture.User(11, "Bert");
        fixture.Sync();
        var closesAt = fixture.Now.AddMinutes(10);
        fixture.State.CloseList(fixture.State.Version, closesAt, AssemblySessionFixture.Actor, fixture.Now);

        fixture.Advance(TimeSpan.FromMinutes(9));
        fixture.Move(10, AssemblySessionFixture.Contra);
        fixture.Advance(TimeSpan.FromMinutes(1));
        var effects = fixture.State.Tick(fixture.Now);
        fixture.Advance(TimeSpan.FromSeconds(1));
        fixture.Move(11, AssemblySessionFixture.Contra);

        await Assert.That(effects.Changed).IsTrue();
        await Assert.That(fixture.State.ListClosedAt).IsEqualTo(closesAt);
        await Assert.That(fixture.Entry(QueueKind.Contra, "Anna").AfterListClosed).IsFalse();
        await Assert.That(fixture.Entry(QueueKind.Contra, "Bert").AfterListClosed).IsTrue();
    }

    [Test]
    public async Task Reopen_clears_late_flags() {
        var fixture = new AssemblySessionFixture();
        fixture.User(10, "Anna");
        fixture.Sync();
        fixture.State.CloseList(fixture.State.Version, null, AssemblySessionFixture.Actor, fixture.Now);
        fixture.Move(10, AssemblySessionFixture.Pro);

        fixture.State.ReopenList(fixture.State.Version, AssemblySessionFixture.Actor, fixture.Now);

        await Assert.That(fixture.Entry(QueueKind.Pro, "Anna").AfterListClosed).IsFalse();
        await Assert.That(fixture.State.ListClosedAt).IsNull();
    }

    [Test]
    public async Task Skip_and_unskip() {
        var fixture = new AssemblySessionFixture();
        fixture.User(10, "Anna");
        fixture.User(11, "Bert");
        fixture.Sync();
        fixture.Move(10, AssemblySessionFixture.Pro);
        fixture.Advance(TimeSpan.FromSeconds(1));
        fixture.Move(11, AssemblySessionFixture.Pro);
        var key = fixture.Entry(QueueKind.Pro, "Anna").Key;

        fixture.State.Skip(fixture.State.Version, key, true, AssemblySessionFixture.Actor, fixture.Now);
        await Assert.That(fixture.State.Suggestion()!.Identity.Name).IsEqualTo("Bert");
        await Assert.That(fixture.Names(QueueKind.Pro)).IsEquivalentTo(["Anna", "Bert"]);

        fixture.State.Skip(fixture.State.Version, key, false, AssemblySessionFixture.Actor, fixture.Now);
        await Assert.That(fixture.State.Suggestion()!.Identity.Name).IsEqualTo("Anna");
    }

    [Test]
    public async Task Dismiss_and_undo() {
        var fixture = new AssemblySessionFixture();
        fixture.User(10, "Anna");
        fixture.Sync();
        fixture.Move(10, AssemblySessionFixture.Pro);
        var key = fixture.Entry(QueueKind.Pro, "Anna").Key;

        var (result, effects) = fixture.State.Dismiss(fixture.State.Version, key, AssemblySessionFixture.Actor, fixture.Now);
        await Assert.That(result.Accepted).IsTrue();
        await Assert.That(effects.Moves).IsEmpty();
        await Assert.That(fixture.Entry(QueueKind.Pro, "Anna").Dismissed).IsTrue();
        await Assert.That(fixture.State.Suggestion()).IsNull();

        fixture.State.UndoDismiss(fixture.State.Version, key, AssemblySessionFixture.Actor, fixture.Now);
        await Assert.That(fixture.Entry(QueueKind.Pro, "Anna").Dismissed).IsFalse();
    }

    [Test]
    public async Task Give_floor_twice_moves_once() {
        var fixture = new AssemblySessionFixture();
        fixture.User(10, "Anna");
        fixture.Sync();
        fixture.Move(10, AssemblySessionFixture.Pro);
        var key = fixture.Entry(QueueKind.Pro, "Anna").Key;

        var (first, firstEffects) = fixture.State.GiveFloor(fixture.State.Version, key, AssemblySessionFixture.Actor, fixture.Now);
        var (second, secondEffects) = fixture.State.GiveFloor(fixture.State.Version, key, AssemblySessionFixture.Actor, fixture.Now);

        await Assert.That(first.Accepted).IsTrue();
        await Assert.That(firstEffects.Moves.Single()).IsEqualTo(new AssemblyMove(10, AssemblySessionFixture.Speaker));
        await Assert.That(second.Accepted).IsFalse();
        await Assert.That(second.MessageKey).IsEqualTo(I18nKey.Ui.AssemblyConsole.Reject.MovePending);
        await Assert.That(secondEffects.Moves).IsEmpty();
    }

    [Test]
    public async Task Stale_version_command_is_rejected() {
        var fixture = new AssemblySessionFixture();
        fixture.User(10, "Anna");
        fixture.User(11, "Bert");
        fixture.Sync();
        fixture.Move(10, AssemblySessionFixture.Pro);
        fixture.Move(11, AssemblySessionFixture.Contra);
        var seenByBoth = fixture.State.Version;

        var (chairA, _) = fixture.State.GiveFloor(seenByBoth, fixture.Entry(QueueKind.Pro, "Anna").Key, AssemblySessionFixture.Actor, fixture.Now);
        var (chairB, effectsB) = fixture.State.GiveFloor(seenByBoth, fixture.Entry(QueueKind.Contra, "Bert").Key, AssemblySessionFixture.Actor, fixture.Now);

        await Assert.That(chairA.Accepted).IsTrue();
        await Assert.That(chairB.Accepted).IsFalse();
        await Assert.That(chairB.MessageKey).IsEqualTo(I18nKey.Ui.AssemblyConsole.Reject.Stale);
        await Assert.That(effectsB.Moves).IsEmpty();
    }

    [Test]
    public async Task Queue_join_does_not_make_commands_stale() {
        var fixture = new AssemblySessionFixture();
        fixture.User(10, "Anna");
        fixture.User(11, "Bert");
        fixture.Sync();
        fixture.Move(10, AssemblySessionFixture.Pro);
        var seen = fixture.State.Version;

        var effects = fixture.Move(11, AssemblySessionFixture.Contra);
        var (result, _) = fixture.State.GiveFloor(seen, fixture.Entry(QueueKind.Pro, "Anna").Key, AssemblySessionFixture.Actor, fixture.Now);

        await Assert.That(effects.Changed).IsTrue();
        await Assert.That(result.Accepted).IsTrue();
    }

    [Test]
    public async Task Move_failure_clears_pending() {
        var fixture = new AssemblySessionFixture();
        fixture.User(10, "Anna");
        fixture.Sync();
        fixture.Move(10, AssemblySessionFixture.Pro);
        var key = fixture.Entry(QueueKind.Pro, "Anna").Key;
        fixture.State.GiveFloor(fixture.State.Version, key, AssemblySessionFixture.Actor, fixture.Now);

        fixture.State.OnMoveFailed(10, new MumbleMoveResult(MumbleMoveOutcome.PermissionDenied, "Move"), fixture.Now);
        var (retry, effects) = fixture.State.GiveFloor(fixture.State.Version, key, AssemblySessionFixture.Actor, fixture.Now);

        await Assert.That(retry.Accepted).IsTrue();
        await Assert.That(effects.Moves.Count).IsEqualTo(1);
        await Assert.That(fixture.State.Snapshot(fixture.Now).Notices.Any(n => n.MessageKey.StartsWith(I18nKey.Ui.AssemblyConsole.Notice.MoveFailed))).IsTrue();
    }

    [Test]
    public async Task Manual_move_into_speaker_channel_consumes_request() {
        var fixture = new AssemblySessionFixture();
        fixture.User(10, "Anna");
        fixture.Sync();
        fixture.Move(10, AssemblySessionFixture.Contra);

        var effects = fixture.Move(10, AssemblySessionFixture.Speaker);

        await Assert.That(effects.Moves).IsEmpty();
        await Assert.That(fixture.Names(QueueKind.Contra)).IsEmpty();
        var floor = fixture.FloorOf(10)!;
        await Assert.That(floor.Role).IsEqualTo(SpeakerRole.Contra);
        await Assert.That(fixture.State.Snapshot(fixture.Now).Notices.First().MessageKey).StartsWith(I18nKey.Ui.AssemblyConsole.Notice.ManualFloor);
    }

    [Test]
    public async Task Done_moves_to_listener_and_logs_speech() {
        var fixture = new AssemblySessionFixture();
        fixture.User(10, "Anna");
        fixture.Sync();
        fixture.Move(10, AssemblySessionFixture.Pro);
        fixture.State.GiveFloor(fixture.State.Version, fixture.Entry(QueueKind.Pro, "Anna").Key, AssemblySessionFixture.Actor, fixture.Now);
        fixture.Move(10, AssemblySessionFixture.Speaker);
        fixture.State.OnSpeaking(10, true, fixture.Now);
        fixture.Advance(TimeSpan.FromSeconds(90));

        var (result, effects) = fixture.State.Done(fixture.State.Version, 10, AssemblySessionFixture.Actor, fixture.Now);
        await Assert.That(result.Accepted).IsTrue();
        await Assert.That(effects.Moves.Single()).IsEqualTo(new AssemblyMove(10, AssemblySessionFixture.Listener));

        var doneAt = fixture.Now;
        fixture.Advance(TimeSpan.FromSeconds(1));
        var left = fixture.Move(10, AssemblySessionFixture.Listener);
        var speech = left.Log.Single();
        await Assert.That(speech.EndedAt).IsEqualTo(doneAt);
        await Assert.That(speech.StartedAt).IsEqualTo(doneAt.AddSeconds(-90));
        await Assert.That(speech.Role).IsEqualTo("Pro");
        await Assert.That(fixture.FloorOf(10)).IsNull();
        await Assert.That(fixture.Names(QueueKind.Pro)).IsEmpty();
    }

    [Test]
    public async Task Give_floor_to_any_user_moves_into_motion_author_channel() {
        var fixture = new AssemblySessionFixture();
        fixture.User(10, "Antragstellerin", channel: AssemblySessionFixture.Lobby);
        fixture.Sync();

        var (result, effects) = fixture.State.GiveFloorToUser(fixture.State.Version, 10, ChannelRole.MotionAuthor, AssemblySessionFixture.Actor, fixture.Now);
        fixture.Move(10, AssemblySessionFixture.MotionAuthor);

        await Assert.That(result.Accepted).IsTrue();
        await Assert.That(effects.Moves.Single()).IsEqualTo(new AssemblyMove(10, AssemblySessionFixture.MotionAuthor));
        var floor = fixture.FloorOf(10)!;
        await Assert.That(floor.Role).IsEqualTo(SpeakerRole.MotionAuthor);
        await Assert.That(floor.Timer.AllottedSeconds).IsEqualTo(300);
    }

    [Test]
    public async Task Give_floor_to_user_rejects_other_targets() {
        var fixture = new AssemblySessionFixture();
        fixture.User(10, "Anna");
        fixture.Sync();

        var (result, _) = fixture.State.GiveFloorToUser(fixture.State.Version, 10, ChannelRole.Pro, AssemblySessionFixture.Actor, fixture.Now);

        await Assert.That(result.MessageKey).IsEqualTo(I18nKey.Ui.AssemblyConsole.Reject.InvalidTarget);
    }

    [Test]
    public async Task Suggestion_prefers_point_then_question_then_pro() {
        var fixture = new AssemblySessionFixture();
        fixture.User(10, "Pro");
        fixture.User(11, "Frage");
        fixture.User(12, "GO");
        fixture.Sync();
        fixture.Move(10, AssemblySessionFixture.Pro);
        fixture.Advance(TimeSpan.FromSeconds(1));
        fixture.Move(11, AssemblySessionFixture.Question);

        await Assert.That(fixture.State.Suggestion()!.Identity.Name).IsEqualTo("Frage");

        fixture.Advance(TimeSpan.FromSeconds(1));
        fixture.Move(12, AssemblySessionFixture.Point);
        await Assert.That(fixture.State.Suggestion()!.Identity.Name).IsEqualTo("GO");
    }

    [Test]
    public async Task Sync_appends_present_members_as_uncertain() {
        var fixture = new AssemblySessionFixture();
        fixture.User(10, "Anna", channel: AssemblySessionFixture.Pro);
        fixture.User(11, "Bert", channel: AssemblySessionFixture.Pro);
        fixture.User(12, "Redner", channel: AssemblySessionFixture.Speaker);

        fixture.Sync();

        var entries = fixture.Queue(QueueKind.Pro).Entries;
        await Assert.That(entries.Select(e => e.Name)).IsEquivalentTo(["Anna", "Bert"]);
        await Assert.That(entries.All(e => e.OrderUncertain)).IsTrue();
        await Assert.That(fixture.FloorOf(12)!.Role).IsEqualTo(SpeakerRole.Manual);
    }

    [Test]
    public async Task Bot_is_ignored() {
        var fixture = new AssemblySessionFixture();
        fixture.Sync();

        var effects = fixture.State.OnUserEntered(new MumbleUser(AssemblySessionFixture.BotSession, "Versammlungsassistent", AssemblySessionFixture.Pro, 1, "bot", true, false), AssemblySessionFixture.Chair, fixture.Now);

        await Assert.That(effects.Changed).IsFalse();
        await Assert.That(fixture.Names(QueueKind.Pro)).IsEmpty();
        await Assert.That(fixture.State.Snapshot(fixture.Now).Users.Any(u => u.Session == AssemblySessionFixture.BotSession)).IsFalse();
    }
}
