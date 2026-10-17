using System;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.AssemblyAssistant.Session;

public record AssemblySessionOptions(AssemblyTimes Times, string WarningText, string ExpiredText, TimeSpan Grace, TimeSpan StartNowAfter) {
    public static AssemblySessionOptions Default { get; } = new(AssemblyTimes.Default, "Noch 30 Sekunden", "Die Redezeit ist abgelaufen",
        TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(20));

    public static AssemblySessionOptions ReadFrom(OptionRepository options) {
        int Seconds(string id, int fallback) {
            return int.TryParse(options.GetGlobalValue(id)?.Value, out var value) && value > 0 ? value : fallback;
        }

        string Text(string id, string fallback) {
            var value = options.GetGlobalValue(id)?.Value;
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        var defaults = AssemblyTimes.Default;
        var times = new AssemblyTimes(
            Seconds("assembly.times.presentation", defaults.Presentation),
            Seconds("assembly.times.speech", defaults.Speech),
            Seconds("assembly.times.question", defaults.Question),
            Seconds("assembly.times.statement", defaults.Statement),
            Seconds("assembly.times.closing_word", defaults.ClosingWord),
            Seconds("assembly.times.candidate_presentation", defaults.CandidatePresentation),
            Seconds("assembly.times.candidate_presentation_repeat", defaults.CandidatePresentationRepeat),
            Seconds("assembly.times.candidate_question", defaults.CandidateQuestion),
            Seconds("assembly.times.candidate_answer", defaults.CandidateAnswer));

        return new AssemblySessionOptions(
            times,
            Text("assembly.messages.warning", Default.WarningText),
            Text("assembly.messages.expired", Default.ExpiredText),
            TimeSpan.FromSeconds(Seconds("assembly.grace_seconds", 30)),
            TimeSpan.FromSeconds(Seconds("assembly.start_now_after_seconds", 20)));
    }
}
