using System;
using System.Globalization;
using System.Linq;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Api.I18n;

namespace Quartermaster.Server.AssemblyAssistant.OpenSlides;

/// <summary><see cref="SummaryKey"/> is an i18n key with parameters; <see cref="Passed"/> is null where only numbers are shown.</summary>
public record PollEvaluationResult(bool? Passed, string SummaryKey, int? WinnerOptionId, bool RunoffNeeded);

/// <summary>
/// Advisory evaluation shown with every result; the chair announces the result. Abstentions never count (Satzung § 9b (9),
/// GO § 6 (2a)): simple majority Ja > Nein, two-thirds Ja >= 2 x Nein (Satzung § 12 (1)), support Ja >= N, acceptance (GO § 7)
/// the option with most Ja if that is more than half of the valid ballots.
/// </summary>
public static class PollEvaluation {
    public static PollEvaluationResult Evaluate(OpenSlidesPollView poll, PollEvaluationKind kind, int threshold) {
        if (kind == PollEvaluationKind.Acceptance)
            return EvaluateAcceptance(poll);

        var (yes, no, abstain) = Counts(poll);
        return kind switch {
            PollEvaluationKind.Simple => Result(yes > no, I18nKey.Ui.AssemblyPollResult.Accepted, I18nKey.Ui.AssemblyPollResult.Rejected, yes, no),
            PollEvaluationKind.TwoThirds => Result(yes > 0 && yes >= 2 * no, I18nKey.Ui.AssemblyPollResult.TwoThirdsReached, I18nKey.Ui.AssemblyPollResult.TwoThirdsMissed, yes, no),
            PollEvaluationKind.Threshold => new PollEvaluationResult(yes >= threshold,
                I18nParams.With(yes >= threshold ? I18nKey.Ui.AssemblyPollResult.ThresholdReached : I18nKey.Ui.AssemblyPollResult.ThresholdMissed,
                    ("yes", Format(yes)), ("threshold", threshold.ToString(CultureInfo.InvariantCulture))),
                null, false),
            _ => new PollEvaluationResult(null,
                I18nParams.With(I18nKey.Ui.AssemblyPollResult.Numbers, ("yes", Format(yes)), ("no", Format(no)), ("abstain", Format(abstain))),
                null, false)
        };
    }

    /// <summary>Ja, Nein and Enthaltung: from the single option of a motion poll, or from the "Ja" and "Nein" options of a topic poll.</summary>
    public static (decimal Yes, decimal No, decimal Abstain) Counts(OpenSlidesPollView poll) {
        var ja = poll.Options.FirstOrDefault(o => string.Equals(o.Text, "Ja", StringComparison.OrdinalIgnoreCase));
        var nein = poll.Options.FirstOrDefault(o => string.Equals(o.Text, "Nein", StringComparison.OrdinalIgnoreCase));
        if (ja != null && nein != null)
            return (ja.Yes, nein.Yes, poll.GlobalOption?.Abstain ?? 0);

        var single = poll.Options.FirstOrDefault();
        return single == null ? (0, 0, 0) : (single.Yes, single.No, single.Abstain);
    }

    private static PollEvaluationResult EvaluateAcceptance(OpenSlidesPollView poll) {
        var ranked = poll.Options.OrderByDescending(o => o.Yes).ToList();
        var valid = poll.VotesValid ?? 0;
        if (ranked.Count == 0 || valid <= 0)
            return Runoff();

        var best = ranked[0];
        if (ranked.Count > 1 && ranked[1].Yes == best.Yes)
            return Runoff();
        if (best.Yes * 2 <= valid)
            return Runoff();

        var summary = I18nParams.With(I18nKey.Ui.AssemblyPollResult.AcceptanceWinner,
            ("option", best.Text ?? best.ContentObjectId ?? best.Id.ToString(CultureInfo.InvariantCulture)), ("yes", Format(best.Yes)), ("valid", Format(valid)));
        return new PollEvaluationResult(true, summary, best.Id, false);
    }

    private static PollEvaluationResult Runoff() {
        return new PollEvaluationResult(null, I18nKey.Ui.AssemblyPollResult.RunoffNeeded, null, true);
    }

    private static PollEvaluationResult Result(bool passed, string passedKey, string failedKey, decimal yes, decimal no) {
        return new PollEvaluationResult(passed, I18nParams.With(passed ? passedKey : failedKey, ("yes", Format(yes)), ("no", Format(no))), null, false);
    }

    private static string Format(decimal value) {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
