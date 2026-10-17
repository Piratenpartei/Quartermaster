using System;

namespace Quartermaster.Api.AssemblyAssistant;

public class AssemblyPollSettingsDTO {
    public int DurationSeconds { get; set; }
    public bool AutoStart { get; set; }
    public bool AutoStop { get; set; }
    public bool AutoPublish { get; set; }
    public bool Project { get; set; }
}

public class AssemblyPollDTO {
    public int Id { get; set; }
    public string Title { get; set; } = "";

    /// <summary>OpenSlides poll state: created, started, finished or published.</summary>
    public string State { get; set; } = "";
    public int LiveVoteCount { get; set; }
    public decimal? VotesValid { get; set; }
    public decimal Yes { get; set; }
    public decimal No { get; set; }
    public decimal Abstain { get; set; }

    /// <summary>End of the voting countdown while the poll runs.</summary>
    public DateTime? EndsAt { get; set; }

    /// <summary>i18n key with parameters for the evaluation, once results exist.</summary>
    public string? ResultKey { get; set; }
    public bool? Passed { get; set; }

    /// <summary>Created by the assistant in this session; only those may be reset or deleted from the console.</summary>
    public bool CreatedHere { get; set; }
}

public class AssemblyAgendaItemDTO {
    public int Id { get; set; }
    public string Label { get; set; } = "";
    public bool IsMotion { get; set; }
}

public class AssemblyOverlayPollDTO {
    public string Title { get; set; } = "";
    public bool Running { get; set; }
    public DateTime? EndsAt { get; set; }
    public int LiveVoteCount { get; set; }
    public decimal Yes { get; set; }
    public decimal No { get; set; }
    public decimal Abstain { get; set; }
    public string? ResultKey { get; set; }
    public bool? Passed { get; set; }

    /// <summary>The result is shown until this time, then the banner disappears.</summary>
    public DateTime? VisibleUntil { get; set; }
}
