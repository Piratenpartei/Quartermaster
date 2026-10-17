using System.Collections.Generic;

namespace Quartermaster.Server.AssemblyAssistant.OpenSlides;

/// <summary>An OpenSlides action the host performs outside the state lock.</summary>
public abstract record OpenSlidesEffect;

public record StopPollEffect(int PollId) : OpenSlidesEffect;

public record PublishPollEffect(int PollId) : OpenSlidesEffect;

/// <summary><see cref="CountdownTime"/> is the absolute end in Unix seconds while running.</summary>
public record SetCountdownEffect(int CountdownId, bool Running, double CountdownTime) : OpenSlidesEffect;

public record ResetPollEffect(int PollId) : OpenSlidesEffect;

public record DeletePollEffect(int PollId) : OpenSlidesEffect;

public record ProjectPollEffect(int PollId, IReadOnlyList<int> ProjectorIds) : OpenSlidesEffect;
