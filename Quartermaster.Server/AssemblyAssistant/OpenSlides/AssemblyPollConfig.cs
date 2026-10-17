using System.Collections.Generic;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Server.AssemblyAssistant.Templates;

namespace Quartermaster.Server.AssemblyAssistant.OpenSlides;

/// <summary>
/// What a session knows about OpenSlides from the start: whether it is connected at all, the poll defaults, the required
/// majority and the competing motions per OpenSlides motion id, the delegates group and the projectors.
/// </summary>
public record AssemblyPollConfig(bool Enabled, PollSettings Settings, IReadOnlyDictionary<int, RequiredMajority> Majorities, int? DelegatesGroupId,
    IReadOnlyList<int> ProjectorIds, IReadOnlyDictionary<int, IReadOnlyList<CompetingMotion>> Competitors) {

    public static AssemblyPollConfig Disabled(PollSettings settings) {
        return new AssemblyPollConfig(false, settings, new Dictionary<int, RequiredMajority>(), null, [], new Dictionary<int, IReadOnlyList<CompetingMotion>>());
    }
}
