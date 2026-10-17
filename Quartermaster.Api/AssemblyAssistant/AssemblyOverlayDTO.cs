using System;
using System.Collections.Generic;

namespace Quartermaster.Api.AssemblyAssistant;

/// <summary>Public state for the OBS overlay: only what the stream audience sees anyway, no sessions or keys.</summary>
public class AssemblyOverlayDTO {
    public DateTime ServerNowUtc { get; set; }
    public bool Active { get; set; }
    public string? ItemTitle { get; set; }
    public string? SpeakerName { get; set; }
    public SpeakerRole? SpeakerRole { get; set; }
    public AssemblyTimerDTO? Timer { get; set; }
    public List<string> NextPro { get; set; } = [];
    public List<string> NextContra { get; set; } = [];
    public bool ElectionRunning { get; set; }
    public AssemblyOverlayPollDTO? Poll { get; set; }
}
