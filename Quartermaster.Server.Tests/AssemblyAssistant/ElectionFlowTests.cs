using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Server.AssemblyAssistant.OpenSlides;
using Quartermaster.Server.AssemblyAssistant.Templates;

namespace Quartermaster.Server.Tests.AssemblyAssistant;

public class ElectionFlowTests {
    private static AssemblySessionFixture Election(AssemblyPollState? polls = null) {
        var fixture = new AssemblySessionFixture(polls: polls);
        fixture.User(10, "Kandidatin");
        fixture.Sync();
        fixture.State.SetItem(fixture.State.Version, "Wahl Beisitzer", null, AssemblyItemKind.Election, AssemblySessionFixture.Actor, fixture.Now);
        return fixture;
    }

    [Test]
    public async Task Candidates_sorted_by_surname() {
        var fixture = Election();

        fixture.State.SetCandidates(fixture.State.Version, ["Zoe Albrecht", "anna Özdemir", "Bernd Müller", " ", "Carla Ober"], AssemblySessionFixture.Actor, fixture.Now);

        await Assert.That(fixture.State.Snapshot(fixture.Now).Candidates).IsEquivalentTo(["Zoe Albrecht", "Bernd Müller", "Carla Ober", "anna Özdemir"]);
    }

    [Test]
    public async Task Choosing_candidate_starts_presentation() {
        var fixture = Election();
        fixture.State.SetCandidates(fixture.State.Version, ["Erika Muster", "Max Beispiel"], AssemblySessionFixture.Actor, fixture.Now);
        fixture.State.SetPhase(fixture.State.Version, AssemblyPhase.CandidateQuestions, AssemblySessionFixture.Actor, fixture.Now);

        var (result, _) = fixture.State.SetCurrentCandidate(fixture.State.Version, 1, AssemblySessionFixture.Actor, fixture.Now);
        var (invalid, _) = fixture.State.SetCurrentCandidate(fixture.State.Version, 5, AssemblySessionFixture.Actor, fixture.Now);

        var snapshot = fixture.State.Snapshot(fixture.Now);
        await Assert.That(result.Accepted).IsTrue();
        await Assert.That(snapshot.CurrentCandidate).IsEqualTo(1);
        await Assert.That(snapshot.Phase).IsEqualTo(AssemblyPhase.CandidatePresentation);
        await Assert.That(invalid.Accepted).IsFalse();
    }

    [Test]
    public async Task Person_in_motion_author_channel_is_candidate() {
        var fixture = Election();

        fixture.Move(10, AssemblySessionFixture.MotionAuthor);

        await Assert.That(fixture.FloorOf(10)!.Role).IsEqualTo(SpeakerRole.Candidate);
        await Assert.That(fixture.State.OverlaySnapshot(fixture.Now).SpeakerRole).IsEqualTo(SpeakerRole.Candidate);
    }

    [Test]
    public async Task Support_reached_at_20() {
        var polls = new AssemblyPollState(true, PollSettings.Default, new Dictionary<int, RequiredMajority>());
        var fixture = Election(polls);
        var plan = new PollPlan("Wahl Beisitzer: Unterstützung Erika Muster", PollEvaluationKind.Threshold, 20, "topic/30", null, null, _ => null!);
        fixture.State.RegisterCreatedPoll(8, plan, AssemblySessionFixture.Actor, fixture.Now);

        var poll = new OpenSlidesPollView(8, plan.Title, "published", "pseudoanonymous", "Y", "topic/30", 23, 23, 0,
            [new OpenSlidesPollOptionView(80, "Ja", null, 20, 0, 0), new OpenSlidesPollOptionView(81, "Nein", null, 3, 0, 0)], null);
        fixture.State.OnOpenSlidesView(new OpenSlidesMeetingView([poll], [], null), fixture.Now);

        var dto = fixture.State.Snapshot(fixture.Now).Polls.Single();
        await Assert.That(dto.Passed).IsTrue();
        await Assert.That(dto.ResultKey).StartsWith(I18nKey.Ui.AssemblyPollResult.ThresholdReached);
        await Assert.That(dto.CreatedHere).IsTrue();
    }

    [Test]
    public async Task Reset_refused_for_foreign_poll() {
        var polls = new AssemblyPollState(true, PollSettings.Default, new Dictionary<int, RequiredMajority>());
        var fixture = Election(polls);
        var plan = new PollPlan("Eigene", PollEvaluationKind.Simple, 0, "topic/30", null, null, _ => null!);
        fixture.State.RegisterCreatedPoll(8, plan, AssemblySessionFixture.Actor, fixture.Now);
        var own = new OpenSlidesPollView(8, "Eigene", "finished", "pseudoanonymous", "Y", "topic/30", 0, 0, 0, [], null);
        var foreign = new OpenSlidesPollView(9, "Fremde", "finished", "pseudoanonymous", "Y", "topic/30", 0, 0, 0, [], null);
        fixture.State.OnOpenSlidesView(new OpenSlidesMeetingView([own, foreign], [], null), fixture.Now);

        var (ownReset, ownEffects) = fixture.State.ResetPoll(fixture.State.Version, 8, AssemblySessionFixture.Actor, fixture.Now);
        var (foreignDelete, _) = fixture.State.DeletePoll(fixture.State.Version, 9, AssemblySessionFixture.Actor, fixture.Now);

        await Assert.That(ownReset.Accepted).IsTrue();
        await Assert.That(((ResetPollEffect)ownEffects.OpenSlides.Single()).PollId).IsEqualTo(8);
        await Assert.That(foreignDelete.MessageKey).IsEqualTo(I18nKey.Ui.AssemblyConsole.Reject.NotOwnPoll);
    }

    [Test]
    public async Task Projection_on_newly_started_poll_when_enabled() {
        var automation = new PollAutomation();
        var settings = PollSettings.Default with { Project = true };
        var created = new OpenSlidesPollView(8, "A", "created", "pseudoanonymous", "Y", "topic/30", null, null, 0, [], null);
        automation.OnView(new OpenSlidesMeetingView([created], [], null), settings, DateTime.UtcNow, [1, 2]);

        var effects = automation.OnView(new OpenSlidesMeetingView([created with { State = "started" }], [], null), settings, DateTime.UtcNow, [1, 2]);

        var project = (ProjectPollEffect)effects.Single();
        await Assert.That(project.PollId).IsEqualTo(8);
        await Assert.That(project.ProjectorIds).IsEquivalentTo([1, 2]);
    }
}
