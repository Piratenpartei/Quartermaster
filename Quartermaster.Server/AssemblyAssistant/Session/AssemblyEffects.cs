using System.Collections.Generic;
using Quartermaster.Data.AssemblyAssistant;
using Quartermaster.Server.AssemblyAssistant.OpenSlides;

namespace Quartermaster.Server.AssemblyAssistant.Session;

public record AssemblyMove(uint Session, uint ChannelId);

public record AssemblyText(uint Session, string Text);

/// <summary>
/// Side effects of a state change, executed by the host outside the state lock. Log entries are upserted by id, so a
/// speech is written once when it starts and again when it ends.
/// </summary>
public record AssemblyEffects(IReadOnlyList<AssemblyMove> Moves, IReadOnlyList<AssemblyText> Texts, IReadOnlyList<AssemblyLogEntry> Log, bool Changed) {
    public static AssemblyEffects None { get; } = new([], [], [], false);

    public IReadOnlyList<OpenSlidesEffect> OpenSlides { get; init; } = [];
}
