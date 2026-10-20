using System;
using System.Collections.Generic;

namespace Quartermaster.Api.AssemblyMotions;

public class AssemblyMotionPortalDTO {
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string EventName { get; set; } = "";
    public DateOnly? EventDate { get; set; }
    public DateTimeOffset OpensAt { get; set; }
    public DateTimeOffset ClosesAt { get; set; }
    public string IntroHtml { get; set; } = "";
    public bool PrioritizationEnabled { get; set; }
    public DateTimeOffset? PrioritizationStartsAt { get; set; }
    public DateTimeOffset? PrioritizationEndsAt { get; set; }
    public List<AssemblyMotionTypeDTO> Types { get; set; } = [];
}

public class AssemblyMotionTypeDTO {
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    public int MinSubmitters { get; set; }
    public DateTimeOffset? Deadline { get; set; }
    public DateTimeOffset? CompetingDeadline { get; set; }
    public RequiredMajority RequiredMajority { get; set; }

    /// <summary>Markdown a new motion of this type starts with in its text; empty for none.</summary>
    public string DefaultText { get; set; } = "";
}

public class AssemblyMotionPortalSettingsDTO {
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string EventName { get; set; } = "";
    public DateOnly? EventDate { get; set; }
    public DateTimeOffset OpensAt { get; set; }
    public DateTimeOffset ClosesAt { get; set; }
    public string IntroMarkdown { get; set; } = "";
    public string IntroHtml { get; set; } = "";
    public bool PrioritizationEnabled { get; set; }
    public DateTimeOffset? PrioritizationStartsAt { get; set; }
    public DateTimeOffset? PrioritizationEndsAt { get; set; }
    public bool WikiMirrorEnabled { get; set; }
    public string? WikiPagePrefix { get; set; }
    public string? WikiAssemblyLabel { get; set; }
    public int? OpenSlidesMeetingId { get; set; }
    public bool ForumMirrorEnabled { get; set; }
    public int? ForumCategoryId { get; set; }

    /// <summary>Warnings of the forum check that ran while saving, such as a publicly readable category; empty otherwise.</summary>
    public List<string> ForumWarnings { get; set; } = [];
    public List<AssemblyMotionTypeSettingsDTO> Types { get; set; } = [];
}

public class AssemblyMotionTypeSettingsDTO {
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    public int MinSubmitters { get; set; }
    public DateTimeOffset? Deadline { get; set; }
    public DateTimeOffset? CompetingDeadline { get; set; }
    public RequiredMajority RequiredMajority { get; set; }
    public string WikiTemplate { get; set; } = "";
    public string DefaultText { get; set; } = "";
    public bool HasMotions { get; set; }
}
