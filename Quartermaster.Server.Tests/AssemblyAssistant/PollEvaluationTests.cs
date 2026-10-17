using System.Collections.Generic;
using System.Threading.Tasks;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Api.I18n;
using Quartermaster.Server.AssemblyAssistant.OpenSlides;

namespace Quartermaster.Server.Tests.AssemblyAssistant;

public class PollEvaluationTests {
    private static OpenSlidesPollView MotionPoll(decimal yes, decimal no, decimal abstain) {
        return new OpenSlidesPollView(7, "Schlussabstimmung", "published", "pseudoanonymous", "YNA", "motion/12", yes + no + abstain, yes + no + abstain, 0,
            [new OpenSlidesPollOptionView(70, null, "motion/12", yes, no, abstain)], null);
    }

    private static OpenSlidesPollView TopicPoll(decimal ja, decimal nein) {
        return new OpenSlidesPollView(8, "Debatte fortsetzen?", "published", "pseudoanonymous", "Y", "topic/30", ja + nein, ja + nein, 0,
            [new OpenSlidesPollOptionView(80, "Ja", null, ja, 0, 0), new OpenSlidesPollOptionView(81, "Nein", null, nein, 0, 0)], null);
    }

    private static OpenSlidesPollView AcceptancePoll(decimal validBallots, params decimal[] yesPerOption) {
        var options = new List<OpenSlidesPollOptionView>();
        for (var i = 0; i < yesPerOption.Length; i++)
            options.Add(new OpenSlidesPollOptionView(90 + i, $"SÄA00{i + 1} Titel", null, yesPerOption[i], 0, 0));

        return new OpenSlidesPollView(9, "Konkurrenz", "published", "pseudoanonymous", "Y", "topic/31", validBallots, validBallots, 0, options, null);
    }

    [Test]
    public async Task Simple_yes_greater_no() {
        var result = PollEvaluation.Evaluate(MotionPoll(30, 20, 40), PollEvaluationKind.Simple, 0);

        await Assert.That(result.Passed).IsTrue();
        await Assert.That(result.SummaryKey).IsEqualTo(I18nParams.With(I18nKey.Ui.AssemblyPollResult.Accepted, ("yes", "30"), ("no", "20")));
    }

    [Test]
    public async Task Simple_tie_fails() {
        var result = PollEvaluation.Evaluate(MotionPoll(20, 20, 5), PollEvaluationKind.Simple, 0);

        await Assert.That(result.Passed).IsFalse();
    }

    [Test]
    public async Task Two_thirds_with_abstentions_34_17_passes() {
        var result = PollEvaluation.Evaluate(MotionPoll(34, 17, 20), PollEvaluationKind.TwoThirds, 0);

        await Assert.That(result.Passed).IsTrue();
        await Assert.That(result.SummaryKey).StartsWith(I18nKey.Ui.AssemblyPollResult.TwoThirdsReached);
    }

    [Test]
    public async Task Two_thirds_33_17_fails() {
        var result = PollEvaluation.Evaluate(MotionPoll(33, 17, 0), PollEvaluationKind.TwoThirds, 0);

        await Assert.That(result.Passed).IsFalse();
    }

    [Test]
    public async Task Two_thirds_without_votes_fails() {
        var result = PollEvaluation.Evaluate(MotionPoll(0, 0, 10), PollEvaluationKind.TwoThirds, 0);

        await Assert.That(result.Passed).IsFalse();
    }

    [Test]
    public async Task Threshold_20_reached() {
        var reached = PollEvaluation.Evaluate(TopicPoll(20, 3), PollEvaluationKind.Threshold, 20);
        var missed = PollEvaluation.Evaluate(TopicPoll(19, 0), PollEvaluationKind.Threshold, 20);

        await Assert.That(reached.Passed).IsTrue();
        await Assert.That(missed.Passed).IsFalse();
    }

    [Test]
    public async Task Topic_poll_reads_ja_nein_options() {
        var counts = PollEvaluation.Counts(TopicPoll(25, 12));
        var result = PollEvaluation.Evaluate(TopicPoll(25, 12), PollEvaluationKind.Simple, 0);

        await Assert.That(counts.Yes).IsEqualTo(25m);
        await Assert.That(counts.No).IsEqualTo(12m);
        await Assert.That(result.Passed).IsTrue();
    }

    [Test]
    public async Task Numbers_only_has_no_verdict() {
        var result = PollEvaluation.Evaluate(MotionPoll(1, 2, 3), PollEvaluationKind.NumbersOnly, 0);

        await Assert.That(result.Passed).IsNull();
        await Assert.That(result.SummaryKey).StartsWith(I18nKey.Ui.AssemblyPollResult.Numbers);
    }

    [Test]
    public async Task Acceptance_winner_above_half() {
        var result = PollEvaluation.Evaluate(AcceptancePoll(60, 40, 25), PollEvaluationKind.Acceptance, 0);

        await Assert.That(result.Passed).IsTrue();
        await Assert.That(result.WinnerOptionId).IsEqualTo(90);
        await Assert.That(result.RunoffNeeded).IsFalse();
    }

    [Test]
    public async Task Acceptance_below_half_needs_runoff() {
        var result = PollEvaluation.Evaluate(AcceptancePoll(60, 30, 25), PollEvaluationKind.Acceptance, 0);

        await Assert.That(result.RunoffNeeded).IsTrue();
        await Assert.That(result.WinnerOptionId).IsNull();
    }

    [Test]
    public async Task Acceptance_tie_needs_runoff() {
        var result = PollEvaluation.Evaluate(AcceptancePoll(60, 40, 40), PollEvaluationKind.Acceptance, 0);

        await Assert.That(result.RunoffNeeded).IsTrue();
    }
}
