using System;

namespace Quartermaster.Api.AssemblyMotions;

public class AssemblyMotionPortalByEventRequest {
    public Guid EventId { get; set; }
}

public class AssemblyMotionPortalSettingsRequest {
    public Guid Id { get; set; }
}

public class AssemblyMotionPortalEnableRequest {
    public Guid EventId { get; set; }
    public DateTimeOffset OpensAt { get; set; }
    public DateTimeOffset ClosesAt { get; set; }
}

public class AssemblyMotionPortalUpdateRequest {
    public Guid Id { get; set; }
    public DateTimeOffset OpensAt { get; set; }
    public DateTimeOffset ClosesAt { get; set; }
    public string IntroMarkdown { get; set; } = "";
    public bool PrioritizationEnabled { get; set; }
    public DateTimeOffset? PrioritizationStartsAt { get; set; }
    public DateTimeOffset? PrioritizationEndsAt { get; set; }
    public bool WikiMirrorEnabled { get; set; }
    public string? WikiPagePrefix { get; set; }
    public string? WikiAssemblyLabel { get; set; }
    public int? OpenSlidesMeetingId { get; set; }
}

public abstract class AssemblyMotionTypeFields {
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    public int MinSubmitters { get; set; } = 1;
    public DateTimeOffset? Deadline { get; set; }
    public DateTimeOffset? CompetingDeadline { get; set; }
    public RequiredMajority RequiredMajority { get; set; }
    public string WikiTemplate { get; set; } = "";
}

public class AssemblyMotionTypeCreateRequest : AssemblyMotionTypeFields {
    public Guid PortalId { get; set; }
}

public class AssemblyMotionTypeUpdateRequest : AssemblyMotionTypeFields {
    public Guid Id { get; set; }
}

public class AssemblyMotionTypeDeleteRequest {
    public Guid Id { get; set; }
}
