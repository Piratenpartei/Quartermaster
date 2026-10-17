using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Server.AssemblyAssistant.OpenSlides;

namespace Quartermaster.Server.Tests.AssemblyAssistant;

public class SuggestionEngineTests {
    private static AssemblySessionFixture DebateWith(int pro, int contra) {
        var fixture = new AssemblySessionFixture();
        for (uint i = 0; i < pro; i++)
            fixture.User(400 + i, $"Pro{i + 1}");
        for (uint i = 0; i < contra; i++)
            fixture.User(500 + i, $"Contra{i + 1}");
        fixture.Sync();
        fixture.State.SetItem(fixture.State.Version, "SÄA001", null, AssemblyItemKind.Motion, AssemblySessionFixture.Actor, fixture.Now);
        fixture.State.SetPhase(fixture.State.Version, AssemblyPhase.Debate, AssemblySessionFixture.Actor, fixture.Now);

        for (uint i = 0; i < pro; i++) {
            fixture.Advance(TimeSpan.FromSeconds(1));
            fixture.Move(400 + i, AssemblySessionFixture.Pro);
        }
        for (uint i = 0; i < contra; i++) {
            fixture.Advance(TimeSpan.FromSeconds(1));
            fixture.Move(500 + i, AssemblySessionFixture.Contra);
        }
        return fixture;
    }

    // Gives the floor to the suggestion and ends the speech, as the chair would.
    private static string SpeakSuggested(AssemblySessionFixture fixture) {
        var entry = fixture.State.Suggestion()!;
        var session = entry.Session;
        var name = entry.Identity.Name;
        fixture.State.GiveFloor(fixture.State.Version, entry.Key, AssemblySessionFixture.Actor, fixture.Now);
        fixture.Move(session, AssemblySessionFixture.Speaker);
        fixture.State.Done(fixture.State.Version, session, AssemblySessionFixture.Actor, fixture.Now);
        fixture.Move(session, AssemblySessionFixture.Listener);
        return name;
    }

    [Test]
    public async Task Alternates_pro_contra_starting_with_pro() {
        var fixture = DebateWith(3, 3);

        var order = Enumerable.Range(0, 6).Select(_ => SpeakSuggested(fixture)).ToList();

        await Assert.That(order).IsEquivalentTo(["Pro1", "Contra1", "Pro2", "Contra2", "Pro3", "Contra3"]);
    }

    [Test]
    public async Task Only_one_side_left_continues_that_side_until_its_five() {
        var fixture = DebateWith(7, 1);

        var order = Enumerable.Range(0, 6).Select(_ => SpeakSuggested(fixture)).ToList();

        await Assert.That(order).IsEquivalentTo(["Pro1", "Contra1", "Pro2", "Pro3", "Pro4", "Pro5"]);
        await Assert.That(fixture.State.Suggestion()).IsNull();
    }

    [Test]
    public async Task Unused_slots_do_not_pass_over() {
        var fixture = DebateWith(7, 0);

        for (var i = 0; i < 5; i++)
            SpeakSuggested(fixture);

        var snapshot = fixture.State.Snapshot(fixture.Now);
        await Assert.That(snapshot.ProCount).IsEqualTo(5);
        await Assert.That(fixture.State.Suggestion()).IsNull();
        await Assert.That(snapshot.RoundComplete).IsTrue();
        await Assert.That(snapshot.WaitingCount).IsEqualTo(2);
    }

    [Test]
    public async Task Round_end_when_both_full() {
        var fixture = DebateWith(5, 5);

        for (var i = 0; i < 10; i++)
            SpeakSuggested(fixture);

        var snapshot = fixture.State.Snapshot(fixture.Now);
        await Assert.That(snapshot.ProCount).IsEqualTo(5);
        await Assert.That(snapshot.ContraCount).IsEqualTo(5);
        await Assert.That(snapshot.RoundComplete).IsTrue();
    }

    [Test]
    public async Task Round_end_when_both_empty() {
        var fixture = DebateWith(1, 1);

        SpeakSuggested(fixture);
        SpeakSuggested(fixture);

        await Assert.That(fixture.State.Snapshot(fixture.Now).RoundComplete).IsTrue();
    }

    [Test]
    public async Task Question_suggested_before_pro() {
        var fixture = DebateWith(1, 0);
        fixture.Connect(fixture.User(300, "Frage", AssemblySessionFixture.Question));

        await Assert.That(fixture.State.Suggestion()!.Identity.Name).IsEqualTo("Frage");
    }

    [Test]
    public async Task Go_not_suggested_during_speech_or_poll() {
        var fixture = DebateWith(2, 0);
        var first = fixture.State.Suggestion()!;
        fixture.State.GiveFloor(fixture.State.Version, first.Key, AssemblySessionFixture.Actor, fixture.Now);
        fixture.Move(first.Session, AssemblySessionFixture.Speaker);
        fixture.Connect(fixture.User(300, "GO", AssemblySessionFixture.Point));

        await Assert.That(fixture.State.Suggestion()!.Identity.Name).IsEqualTo("Pro2");

        fixture.State.Done(fixture.State.Version, first.Session, AssemblySessionFixture.Actor, fixture.Now);
        fixture.Move(first.Session, AssemblySessionFixture.Listener);
        await Assert.That(fixture.State.Suggestion()!.Identity.Name).IsEqualTo("GO");
    }

    [Test]
    public async Task Go_not_suggested_while_poll_runs() {
        var polls = new AssemblyPollState(true, PollSettings.Default, new Dictionary<int, RequiredMajority>());
        var fixture = new AssemblySessionFixture(polls: polls);
        fixture.User(300, "GO");
        fixture.Sync();
        fixture.Move(300, AssemblySessionFixture.Point);
        var running = new OpenSlidesPollView(8, "Abstimmung", "started", "pseudoanonymous", "Y", "topic/30", null, null, 0, [], null);

        fixture.State.OnOpenSlidesView(new OpenSlidesMeetingView([running], [], null), fixture.Now);

        await Assert.That(fixture.State.Suggestion()).IsNull();
    }

    [Test]
    public async Task General_debate_single_queue_in_entry_order() {
        var fixture = new AssemblySessionFixture();
        fixture.User(10, "Erste");
        fixture.User(11, "Zweite");
        fixture.User(12, "Dritte");
        fixture.Sync();
        fixture.State.SetItem(fixture.State.Version, "Aussprache", null, AssemblyItemKind.GeneralDebate, AssemblySessionFixture.Actor, fixture.Now);
        fixture.Move(10, AssemblySessionFixture.Contra);
        fixture.Advance(TimeSpan.FromSeconds(1));
        fixture.Move(11, AssemblySessionFixture.Pro);
        fixture.Advance(TimeSpan.FromSeconds(1));
        fixture.Move(12, AssemblySessionFixture.Question);

        var order = Enumerable.Range(0, 3).Select(_ => SpeakSuggested(fixture)).ToList();

        await Assert.That(fixture.State.Snapshot(fixture.Now).Phase).IsEqualTo(AssemblyPhase.GeneralDebate);
        await Assert.That(order).IsEquivalentTo(["Erste", "Zweite", "Dritte"]);
    }

    [Test]
    public async Task Questions_phase_suggests_only_questions() {
        var fixture = DebateWith(1, 1);
        fixture.State.SetPhase(fixture.State.Version, AssemblyPhase.Questions, AssemblySessionFixture.Actor, fixture.Now);

        await Assert.That(fixture.State.Suggestion()).IsNull();

        fixture.Connect(fixture.User(300, "Frage", AssemblySessionFixture.Question));
        await Assert.That(fixture.State.Suggestion()!.Identity.Name).IsEqualTo("Frage");
    }

    [Test]
    public async Task Overlay_lists_next_two_per_side() {
        var fixture = DebateWith(3, 1);

        var overlay = fixture.State.OverlaySnapshot(fixture.Now);

        await Assert.That(overlay.NextPro).IsEquivalentTo(["Pro1", "Pro2"]);
        await Assert.That(overlay.NextContra).IsEquivalentTo(["Contra1"]);
    }
}
