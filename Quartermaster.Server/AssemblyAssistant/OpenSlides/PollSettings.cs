using Quartermaster.Data.Options;

namespace Quartermaster.Server.AssemblyAssistant.OpenSlides;

/// <summary>Poll behaviour of a running session; the options are only the defaults, the chair changes them live in the console.</summary>
public record PollSettings(int DurationSeconds, bool AutoStart, bool AutoStop, bool AutoPublish, bool Project = false) {
    public static PollSettings Default { get; } = new(70, true, true, true);

    public static PollSettings ReadFrom(OptionRepository options) {
        bool Flag(string id, bool fallback) {
            var value = options.GetGlobalValue(id)?.Value;
            return bool.TryParse(value?.Trim(), out var parsed) ? parsed : fallback;
        }

        var duration = int.TryParse(options.GetGlobalValue("assembly.poll.duration_seconds")?.Value, out var seconds) && seconds > 0
            ? seconds
            : Default.DurationSeconds;

        return new PollSettings(
            duration,
            Flag("assembly.poll.auto_start", Default.AutoStart),
            Flag("assembly.poll.auto_stop", Default.AutoStop),
            Flag("assembly.poll.auto_publish", Default.AutoPublish),
            Flag("assembly.poll.project", false));
    }
}
