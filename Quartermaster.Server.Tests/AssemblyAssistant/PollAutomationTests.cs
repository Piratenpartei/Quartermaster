using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AssemblyAssistant;
using Quartermaster.Server.AssemblyAssistant.OpenSlides;
using Quartermaster.Server.AssemblyAssistant.Session;

namespace Quartermaster.Server.Tests.AssemblyAssistant;

public class PollAutomationTests {
    private static readonly DateTime T0 = new(2026, 11, 28, 14, 0, 0, DateTimeKind.Utc);
    private static readonly PollSettings Settings = new(70, true, true, true);

    private static OpenSlidesPollView Poll(int id, string state, string contentObject = "topic/30", decimal ja = 0, decimal nein = 0) {
        return new OpenSlidesPollView(id, $"Abstimmung {id}", state, "pseudoanonymous", "Y", contentObject, ja + nein, ja + nein, 0,
            [new OpenSlidesPollOptionView(id * 10, "Ja", null, ja, 0, 0), new OpenSlidesPollOptionView(id * 10 + 1, "Nein", null, nein, 0, 0)], null);
    }

    private static OpenSlidesPollView MotionPoll(int id, string state, int motionId, decimal yes, decimal no) {
        return new OpenSlidesPollView(id, $"Schlussabstimmung {id}", state, "pseudoanonymous", "YNA", $"motion/{motionId}", yes + no, yes + no, 0,
            [new OpenSlidesPollOptionView(id * 10, null, $"motion/{motionId}", yes, no, 0)], null);
    }

    private static OpenSlidesMeetingView View(OpenSlidesCountdownView? countdown, params OpenSlidesPollView[] polls) {
        return new OpenSlidesMeetingView(polls, [], countdown);
    }

    private static OpenSlidesCountdownView Countdown(bool running, DateTime end, int defaultTime = 60) {
        return new OpenSlidesCountdownView(2, running, PollAutomation.ToUnix(end), defaultTime);
    }

    [Test]
    public async Task Different_duration_sets_countdown_after_start() {
        var automation = new PollAutomation();
        automation.OnView(View(Countdown(false, T0), Poll(8, "created")), Settings, T0);

        var effects = automation.OnView(View(Countdown(true, T0.AddSeconds(60)), Poll(8, "started")), Settings, T0);

        var countdown = (SetCountdownEffect)effects.Single();
        await Assert.That(countdown.CountdownId).IsEqualTo(2);
        await Assert.That(countdown.Running).IsTrue();
        await Assert.That(countdown.CountdownTime).IsEqualTo(PollAutomation.ToUnix(T0.AddSeconds(70)));
    }

    [Test]
    public async Task Same_duration_leaves_countdown_alone() {
        var automation = new PollAutomation();
        automation.OnView(View(Countdown(false, T0, 70), Poll(8, "created")), Settings, T0);

        var effects = automation.OnView(View(Countdown(true, T0.AddSeconds(70), 70), Poll(8, "started")), Settings, T0);

        await Assert.That(effects).IsEmpty();
    }

    [Test]
    public async Task Poll_already_running_on_connect_keeps_countdown() {
        var automation = new PollAutomation();

        var effects = automation.OnView(View(Countdown(true, T0.AddSeconds(30)), Poll(8, "started")), Settings, T0);

        await Assert.That(effects).IsEmpty();
    }

    [Test]
    public async Task Auto_stop_sends_stop_once() {
        var automation = new PollAutomation();
        var view = View(Countdown(true, T0.AddSeconds(70)), Poll(8, "started"));
        automation.OnView(view, Settings, T0);

        var early = automation.Tick(view, Settings, T0.AddSeconds(69));
        var due = automation.Tick(view, Settings, T0.AddSeconds(70));
        var again = automation.Tick(view, Settings, T0.AddSeconds(70.25));

        await Assert.That(early).IsEmpty();
        await Assert.That(((StopPollEffect)due.Single()).PollId).IsEqualTo(8);
        await Assert.That(again).IsEmpty();
    }

    [Test]
    public async Task Auto_stop_off_sends_nothing() {
        var automation = new PollAutomation();
        var view = View(Countdown(true, T0), Poll(8, "started"));
        automation.OnView(view, Settings, T0);

        var effects = automation.Tick(view, Settings with { AutoStop = false }, T0.AddSeconds(5));

        await Assert.That(effects).IsEmpty();
    }

    [Test]
    public async Task Auto_publish_after_finished_once() {
        var automation = new PollAutomation();
        automation.OnView(View(Countdown(true, T0), Poll(8, "started")), Settings, T0);

        var first = automation.OnView(View(Countdown(false, T0), Poll(8, "finished")), Settings, T0);
        var second = automation.OnView(View(Countdown(false, T0), Poll(8, "finished")), Settings, T0);
        var off = new PollAutomation().OnView(View(null, Poll(9, "finished")), Settings with { AutoPublish = false }, T0);

        await Assert.That(((PublishPollEffect)first.Single()).PollId).IsEqualTo(8);
        await Assert.That(second).IsEmpty();
        await Assert.That(off).IsEmpty();
    }

    [Test]
    public async Task Finished_poll_on_connect_is_not_published() {
        var automation = new PollAutomation();

        var first = automation.OnView(View(null, Poll(7, "finished")), Settings, T0);
        var again = automation.OnView(View(null, Poll(7, "finished")), Settings, T0);

        await Assert.That(first).IsEmpty();
        await Assert.That(again).IsEmpty();
    }

    [Test]
    public async Task Extend_moves_countdown_time() {
        var automation = new PollAutomation();

        var effects = automation.Extend(View(Countdown(true, T0.AddSeconds(40)), Poll(8, "started")), 30);
        var stopped = automation.Extend(View(Countdown(false, T0), Poll(8, "started")), 30);

        var countdown = (SetCountdownEffect)effects.Single();
        await Assert.That(countdown.CountdownTime).IsEqualTo(PollAutomation.ToUnix(T0.AddSeconds(70)));
        await Assert.That(stopped).IsEmpty();
    }

    [Test]
    public async Task Manual_poll_gets_same_handling() {
        var automation = new PollAutomation();
        automation.OnView(View(Countdown(false, T0)), Settings, T0);

        var started = automation.OnView(View(Countdown(true, T0.AddSeconds(60)), Poll(42, "started")), Settings, T0);
        var stop = automation.Tick(View(Countdown(true, T0.AddSeconds(70)), Poll(42, "started")), Settings, T0.AddSeconds(71));

        await Assert.That(started.Single()).IsTypeOf<SetCountdownEffect>();
        await Assert.That(((StopPollEffect)stop.Single()).PollId).IsEqualTo(42);
    }

    [Test]
    public async Task Motion_poll_uses_type_majority() {
        var state = new AssemblyPollState(true, Settings, new Dictionary<int, RequiredMajority> { [12] = RequiredMajority.TwoThirds });

        state.OnView(View(null, MotionPoll(7, "finished", 12, 33, 17), MotionPoll(8, "finished", 13, 33, 17)), T0);

        var twoThirds = state.Evaluate(state.FindPoll(7)!)!;
        var simple = state.Evaluate(state.FindPoll(8)!)!;
        await Assert.That(twoThirds.Passed).IsFalse();
        await Assert.That(twoThirds.SummaryKey).StartsWith(I18nKey.Ui.AssemblyPollResult.TwoThirdsMissed);
        await Assert.That(simple.Passed).IsTrue();
    }

    [Test]
    public async Task Registered_threshold_is_used_for_topic_poll() {
        var state = new AssemblyPollState(true, Settings, new Dictionary<int, RequiredMajority>());
        state.RegisterEvaluation(8, PollEvaluationKind.Threshold, 20);

        state.OnView(View(null, Poll(8, "published", ja: 21)), T0);

        await Assert.That(state.Evaluate(state.FindPoll(8)!)!.Passed).IsTrue();
    }

    [Test]
    public async Task Published_poll_is_logged_and_shown_on_overlay_for_15_seconds() {
        var polls = new AssemblyPollState(true, Settings, new Dictionary<int, RequiredMajority>());
        var fixture = new AssemblySessionState(Guid.NewGuid(), AssemblySessionFixture.Channels, AssemblySessionOptions.Default, polls);
        fixture.OnOpenSlidesView(View(null, Poll(8, "finished", ja: 30, nein: 10)), T0);

        var effects = fixture.OnOpenSlidesView(View(null, Poll(8, "published", ja: 30, nein: 10)), T0);

        var log = effects.Log.Single();
        await Assert.That(log.Kind).IsEqualTo(AssemblyLogKind.Poll);
        await Assert.That(log.OpenSlidesPollId).IsEqualTo(8);
        await Assert.That(log.Detail).StartsWith("Abstimmung 8\n");
        var during = fixture.OverlaySnapshot(T0.AddSeconds(14)).Poll!;
        await Assert.That(during.Running).IsFalse();
        await Assert.That(during.Yes).IsEqualTo(30m);
        await Assert.That(during.VisibleUntil).IsEqualTo(T0.AddSeconds(15));
        await Assert.That(fixture.OverlaySnapshot(T0.AddSeconds(16)).Poll).IsNull();
    }

    [Test]
    public async Task Running_poll_banner_has_countdown_and_live_count() {
        var polls = new AssemblyPollState(true, Settings, new Dictionary<int, RequiredMajority>());
        var fixture = new AssemblySessionState(Guid.NewGuid(), AssemblySessionFixture.Channels, AssemblySessionOptions.Default, polls);
        var running = Poll(8, "started") with { LiveVoteCount = 42 };

        fixture.OnOpenSlidesView(View(Countdown(true, T0.AddSeconds(50)), running), T0);

        var banner = fixture.OverlaySnapshot(T0).Poll!;
        await Assert.That(banner.Running).IsTrue();
        await Assert.That(banner.LiveVoteCount).IsEqualTo(42);
        await Assert.That(banner.EndsAt).IsEqualTo(T0.AddSeconds(50));
    }

    [Test]
    public async Task Select_agenda_item_sets_title_and_kind() {
        var polls = new AssemblyPollState(true, Settings, new Dictionary<int, RequiredMajority>());
        var fixture = new AssemblySessionState(Guid.NewGuid(), AssemblySessionFixture.Channels, AssemblySessionOptions.Default, polls);
        fixture.OnOpenSlidesView(new OpenSlidesMeetingView([],
            [new OpenSlidesAgendaItemView(20, "4.1", null, 10, "motion/12", "Änderung der Geschäftsordnung", "SÄA001")], null), T0);

        var (result, _) = fixture.SelectAgendaItem(fixture.Version, 20, Guid.NewGuid(), T0);
        var (missing, _) = fixture.SelectAgendaItem(fixture.Version, 99, Guid.NewGuid(), T0);

        await Assert.That(result.Accepted).IsTrue();
        await Assert.That(fixture.ItemTitle).IsEqualTo("4.1 SÄA001: Änderung der Geschäftsordnung");
        await Assert.That(fixture.ItemKind).IsEqualTo(AssemblyItemKind.Motion);
        await Assert.That(fixture.OpenSlidesAgendaItemId).IsEqualTo(20);
        await Assert.That(missing.MessageKey).IsEqualTo(I18nKey.Ui.AssemblyConsole.Reject.AgendaItemNotFound);
    }

    [Test]
    public async Task Stop_command_requires_started_poll() {
        var polls = new AssemblyPollState(true, Settings, new Dictionary<int, RequiredMajority>());
        var fixture = new AssemblySessionState(Guid.NewGuid(), AssemblySessionFixture.Channels, AssemblySessionOptions.Default, polls);
        fixture.OnOpenSlidesView(View(null, Poll(8, "started"), Poll(9, "finished")), T0);

        var (stop, stopEffects) = fixture.StopPoll(fixture.Version, 8, Guid.NewGuid(), T0);
        var (wrong, _) = fixture.StopPoll(fixture.Version, 9, Guid.NewGuid(), T0);

        await Assert.That(stop.Accepted).IsTrue();
        await Assert.That(((StopPollEffect)stopEffects.OpenSlides.Single()).PollId).IsEqualTo(8);
        await Assert.That(wrong.MessageKey).IsEqualTo(I18nKey.Ui.AssemblyConsole.Reject.PollNotFound);
    }
}
