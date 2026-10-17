using System;
using System.Collections.Generic;

namespace Quartermaster.Api.AssemblyAssistant;

public class AssemblySnapshotDTO {
    public long Version { get; set; }
    public DateTime ServerNowUtc { get; set; }
    public Guid SessionId { get; set; }
    public Guid EventId { get; set; }
    public bool CanControl { get; set; }
    public string MumbleState { get; set; } = "";
    public string? MumbleError { get; set; }
    public string? ItemTitle { get; set; }
    public AssemblyItemKind ItemKind { get; set; }
    public int? MotionSpeechMinutes { get; set; }
    public DateTime? ListClosedAt { get; set; }
    public DateTime? ListClosesAt { get; set; }
    public string? SuggestedKey { get; set; }
    public List<AssemblyQueueDTO> Queues { get; set; } = [];
    public List<AssemblyFloorDTO> Floor { get; set; } = [];
    public List<AssemblyUserDTO> Users { get; set; } = [];
    public List<uint> SpeakingSessions { get; set; } = [];
    public List<AssemblyNoticeDTO> Notices { get; set; } = [];

    /// <summary>Disabled (no OpenSlides meeting for the event), Connecting, Connected or Disconnected.</summary>
    public string OpenSlidesState { get; set; } = "Disabled";
    public string? OpenSlidesError { get; set; }
    public int? CurrentAgendaItemId { get; set; }
    public AssemblyPollSettingsDTO PollSettings { get; set; } = new();
    public List<AssemblyPollDTO> Polls { get; set; } = [];
    public List<AssemblyAgendaItemDTO> AgendaItems { get; set; } = [];
    public AssemblyPhase Phase { get; set; }
    public int Round { get; set; }
    public int ProCount { get; set; }
    public int ContraCount { get; set; }
    public int SpeechesPerSide { get; set; }

    /// <summary>No side has both a free slot and a waiting request: the chair asks whether the debate continues.</summary>
    public bool RoundComplete { get; set; }

    /// <summary>Pro and Contra requests still waiting, shown with the round end.</summary>
    public int WaitingCount { get; set; }
    public bool GoActive { get; set; }
    public bool GoObjection { get; set; }
    public bool RepeatPresentation { get; set; }
    public int Questioners { get; set; }
    public int MaxQuestioners { get; set; }
    public bool ElectionRunning { get; set; }
    public bool CurrentIsMotion { get; set; }
    public bool HasCompetitors { get; set; }
    public bool ProjectorConfigured { get; set; }

    /// <summary>Candidates of the current election in presentation order (by surname).</summary>
    public List<string> Candidates { get; set; } = [];
    public int? CurrentCandidate { get; set; }
}

public class AssemblyQueueDTO {
    public QueueKind Kind { get; set; }
    public bool ChannelMapped { get; set; }
    public List<AssemblyQueueEntryDTO> Entries { get; set; } = [];
}

public class AssemblyQueueEntryDTO {
    public string Key { get; set; } = "";
    public uint Session { get; set; }
    public string Name { get; set; } = "";
    public DateTime EnteredAt { get; set; }
    public bool Skipped { get; set; }
    public bool Dismissed { get; set; }
    public bool AfterListClosed { get; set; }
    public bool OrderUncertain { get; set; }
    public bool MovePending { get; set; }
}

public class AssemblyTimerDTO {
    public int? AllottedSeconds { get; set; }
    public DateTime? StartedAt { get; set; }

    /// <summary>End of the speaking time while the timer runs; null while it waits for the first voice packet, is paused or untimed.</summary>
    public DateTime? EndsAt { get; set; }

    /// <summary>Remaining seconds while not running (waiting or paused); negative = overrun.</summary>
    public int? RemainingSeconds { get; set; }
    public bool Paused { get; set; }
    public bool OfferStartNow { get; set; }
}

public class AssemblyFloorDTO {
    public uint Session { get; set; }
    public string Name { get; set; } = "";
    public ChannelRole Channel { get; set; }
    public SpeakerRole Role { get; set; }
    public DateTime ArrivedAt { get; set; }
    public bool DonePending { get; set; }
    public AssemblyTimerDTO Timer { get; set; } = new();
}

public class AssemblyUserDTO {
    public uint Session { get; set; }
    public string Name { get; set; } = "";
    public ChannelRole? ChannelRole { get; set; }
}

public class AssemblyNoticeDTO {
    public DateTime At { get; set; }
    public string MessageKey { get; set; } = "";
}
