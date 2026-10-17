using System;
using System.Linq;
using System.Threading.Tasks;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Api.I18n;

namespace Quartermaster.Server.Tests.AssemblyAssistant;

public class AssemblyFlowTests {
    private static AssemblySessionFixture Motion() {
        var fixture = new AssemblySessionFixture();
        fixture.User(10, "Antragstellerin");
        fixture.User(11, "Pro1");
        fixture.User(12, "Contra1");
        fixture.User(13, "GO");
        fixture.User(14, "Pro2");
        fixture.Sync();
        fixture.State.SetItem(fixture.State.Version, "SÄA001", null, AssemblyItemKind.Motion, AssemblySessionFixture.Actor, fixture.Now);
        return fixture;
    }

    private static void Phase(AssemblySessionFixture fixture, AssemblyPhase phase) {
        fixture.State.SetPhase(fixture.State.Version, phase, AssemblySessionFixture.Actor, fixture.Now);
    }

    private static void Speak(AssemblySessionFixture fixture, uint session, uint requestChannel) {
        fixture.Move(session, requestChannel);
        fixture.Move(session, AssemblySessionFixture.Speaker);
        fixture.State.Done(fixture.State.Version, session, AssemblySessionFixture.Actor, fixture.Now);
        fixture.Move(session, AssemblySessionFixture.Listener);
    }

    private static int? MotionAuthorTime(AssemblySessionFixture fixture) {
        fixture.Move(10, AssemblySessionFixture.MotionAuthor);
        var allotted = fixture.FloorOf(10)!.Timer.AllottedSeconds;
        fixture.Move(10, AssemblySessionFixture.Listener);
        return allotted;
    }

    [Test]
    public async Task Motion_item_starts_with_presentation() {
        var fixture = Motion();

        var snapshot = fixture.State.Snapshot(fixture.Now);

        await Assert.That(snapshot.Phase).IsEqualTo(AssemblyPhase.Presentation);
        await Assert.That(snapshot.Round).IsEqualTo(0);
    }

    [Test]
    public async Task Motion_author_time_follows_phase() {
        var fixture = Motion();

        var presentation = MotionAuthorTime(fixture);
        Phase(fixture, AssemblyPhase.Questions);
        var answers = MotionAuthorTime(fixture);
        Phase(fixture, AssemblyPhase.ClosingWord);
        var closing = MotionAuthorTime(fixture);

        await Assert.That(presentation).IsEqualTo(300);
        await Assert.That(answers).IsNull();
        await Assert.That(closing).IsEqualTo(180);
    }

    [Test]
    public async Task Statement_after_continue_then_next_round() {
        var fixture = Motion();
        Phase(fixture, AssemblyPhase.Debate);
        Speak(fixture, 11, AssemblySessionFixture.Pro);
        Speak(fixture, 12, AssemblySessionFixture.Contra);

        fixture.State.StartNextRound(fixture.State.Version, AssemblySessionFixture.Actor, fixture.Now);
        var statement = fixture.State.Snapshot(fixture.Now);
        var statementTime = MotionAuthorTime(fixture);
        Phase(fixture, AssemblyPhase.Debate);
        var nextRound = fixture.State.Snapshot(fixture.Now);

        await Assert.That(statement.Phase).IsEqualTo(AssemblyPhase.Statement);
        await Assert.That(statementTime).IsEqualTo(60);
        await Assert.That(nextRound.Round).IsEqualTo(2);
        await Assert.That(nextRound.ProCount).IsEqualTo(0);
        await Assert.That(nextRound.ContraCount).IsEqualTo(0);
    }

    [Test]
    public async Task Go_interruption_resumes_counters() {
        var fixture = Motion();
        Phase(fixture, AssemblyPhase.Debate);
        Speak(fixture, 11, AssemblySessionFixture.Pro);

        fixture.Move(13, AssemblySessionFixture.Point);
        fixture.Move(13, AssemblySessionFixture.Speaker);
        var during = fixture.State.Snapshot(fixture.Now);
        var goTimer = fixture.FloorOf(13)!.Timer.AllottedSeconds;
        fixture.State.Done(fixture.State.Version, 13, AssemblySessionFixture.Actor, fixture.Now);
        fixture.Move(13, AssemblySessionFixture.Listener);
        fixture.State.StartGoObjection(fixture.State.Version, AssemblySessionFixture.Actor, fixture.Now);
        Speak(fixture, 14, AssemblySessionFixture.Pro);
        fixture.State.ResumeAfterGo(fixture.State.Version, AssemblySessionFixture.Actor, fixture.Now);
        var after = fixture.State.Snapshot(fixture.Now);

        await Assert.That(during.GoActive).IsTrue();
        await Assert.That(during.SuggestedKey).IsNull();
        await Assert.That(goTimer).IsNull();
        await Assert.That(after.GoActive).IsFalse();
        await Assert.That(after.Phase).IsEqualTo(AssemblyPhase.Debate);
        await Assert.That(after.ProCount).IsEqualTo(1);
    }

    [Test]
    public async Task Go_accepted_without_objection_is_logged() {
        var fixture = Motion();
        Phase(fixture, AssemblyPhase.Debate);
        fixture.Move(13, AssemblySessionFixture.Point);
        fixture.Move(13, AssemblySessionFixture.Speaker);

        var (result, effects) = fixture.State.RecordGoAccepted(fixture.State.Version, AssemblySessionFixture.Actor, fixture.Now);

        await Assert.That(result.Accepted).IsTrue();
        await Assert.That(effects.Log.Single().Detail).IsEqualTo(I18nKey.Ui.AssemblyConsole.Notice.GoAccepted);
        await Assert.That(fixture.State.Snapshot(fixture.Now).GoActive).IsFalse();
    }

    [Test]
    public async Task Candidate_questions_limited_to_five() {
        var fixture = new AssemblySessionFixture();
        for (uint i = 0; i < 7; i++)
            fixture.User(400 + i, $"Frage{i + 1}");
        fixture.Sync();
        fixture.State.SetItem(fixture.State.Version, "Wahl Beisitzer", null, AssemblyItemKind.Election, AssemblySessionFixture.Actor, fixture.Now);
        Phase(fixture, AssemblyPhase.CandidateQuestions);

        for (uint i = 0; i < 5; i++) {
            fixture.Advance(TimeSpan.FromSeconds(1));
            Speak(fixture, 400 + i, AssemblySessionFixture.Question);
        }
        fixture.Move(405, AssemblySessionFixture.Question);
        var snapshot = fixture.State.Snapshot(fixture.Now);

        await Assert.That(snapshot.Questioners).IsEqualTo(5);
        await Assert.That(fixture.State.Suggestion()).IsNull();
    }

    [Test]
    public async Task Candidate_times_follow_election_rules() {
        var fixture = new AssemblySessionFixture();
        fixture.User(10, "Kandidatin");
        fixture.User(11, "Fragender");
        fixture.Sync();
        fixture.State.SetItem(fixture.State.Version, "Wahl", null, AssemblyItemKind.Election, AssemblySessionFixture.Actor, fixture.Now);

        var presentation = MotionAuthorTime(fixture);
        fixture.State.SetRepeatPresentation(fixture.State.Version, true, AssemblySessionFixture.Actor, fixture.Now);
        var repeat = MotionAuthorTime(fixture);
        Phase(fixture, AssemblyPhase.CandidateQuestions);
        var answer = MotionAuthorTime(fixture);
        fixture.Move(11, AssemblySessionFixture.Question);
        fixture.Move(11, AssemblySessionFixture.Speaker);
        var question = fixture.FloorOf(11)!.Timer.AllottedSeconds;

        await Assert.That(presentation).IsEqualTo(300);
        await Assert.That(repeat).IsEqualTo(180);
        await Assert.That(answer).IsEqualTo(120);
        await Assert.That(question).IsEqualTo(30);
    }

    [Test]
    public async Task Election_marker_reaches_overlay() {
        var fixture = new AssemblySessionFixture();
        fixture.Sync();

        fixture.State.SetElectionRunning(fixture.State.Version, true, AssemblySessionFixture.Actor, fixture.Now);

        await Assert.That(fixture.State.OverlaySnapshot(fixture.Now).ElectionRunning).IsTrue();
        await Assert.That(fixture.State.Snapshot(fixture.Now).Notices.First().MessageKey).IsEqualTo(I18nKey.Ui.AssemblyConsole.Notice.ElectionStarted);
    }

    [Test]
    public async Task New_item_resets_flow() {
        var fixture = Motion();
        Phase(fixture, AssemblyPhase.Debate);
        Speak(fixture, 11, AssemblySessionFixture.Pro);

        fixture.State.SetItem(fixture.State.Version, "Aussprache", null, AssemblyItemKind.GeneralDebate, AssemblySessionFixture.Actor, fixture.Now);
        var snapshot = fixture.State.Snapshot(fixture.Now);

        await Assert.That(snapshot.Phase).IsEqualTo(AssemblyPhase.GeneralDebate);
        await Assert.That(snapshot.ProCount).IsEqualTo(0);
        await Assert.That(snapshot.Round).IsEqualTo(0);
    }
}
