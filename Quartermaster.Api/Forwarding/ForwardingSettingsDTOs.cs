using System;
using System.Collections.Generic;

namespace Quartermaster.Api.Forwarding;

/// <summary>A chapter's Redmine connection. The API key is write-only: only whether one is stored comes back.</summary>
public class RedmineConnectionDTO {
    public Guid ChapterId { get; set; }
    public string ChapterName { get; set; } = "";
    public string BaseUrl { get; set; } = "";
    public bool HasApiKey { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class RedmineConnectionSaveRequest {
    public Guid ChapterId { get; set; }
    public string BaseUrl { get; set; } = "";

    /// <summary>Null or empty keeps the stored key.</summary>
    public string? ApiKey { get; set; }
}

public class ForwardingChapterRequest {
    public Guid ChapterId { get; set; }
}

public class ConnectionCheckResultDTO {
    public bool Ok { get; set; }
    public string Message { get; set; } = "";
}

public class IdNameDTO {
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public class RedmineLookupDTO {
    public List<IdNameDTO> Projects { get; set; } = [];
    public List<IdNameDTO> Trackers { get; set; } = [];
    public List<IdNameDTO> Statuses { get; set; } = [];
    public List<IdNameDTO> Priorities { get; set; } = [];
}

public class ForwardingCustomFieldDTO {
    public int FieldId { get; set; }
    public string Template { get; set; } = "";
}

public class ForwardingTargetDTO {
    public ForwardingPurpose Purpose { get; set; }
    public int ProjectId { get; set; }
    public int TrackerId { get; set; }
    public int? PriorityId { get; set; }
    public int? AssigneeId { get; set; }
    public List<ForwardingCustomFieldDTO> CustomFields { get; set; } = [];
    public int? NoteStatusId { get; set; }
    public int? NoteAssigneeId { get; set; }
}

public class ForwardingChapterSettingDTO {
    public Guid ChapterId { get; set; }
    public string ChapterName { get; set; } = "";
    public bool IsFederal { get; set; }
    public bool IsStateChapter { get; set; }
    public ForwardingMode? ApplicationMode { get; set; }
    public ForwardingMode? ReductionMode { get; set; }
    public string? FailureEmail { get; set; }
    public List<ForwardingTargetDTO> Targets { get; set; } = [];

    /// <summary>Whether a Redmine connection is found for the chapter, its own or inherited.</summary>
    public bool HasConnection { get; set; }
}

public class ForwardingChapterSaveRequest {
    public Guid ChapterId { get; set; }
    public ForwardingMode? ApplicationMode { get; set; }
    public ForwardingMode? ReductionMode { get; set; }
    public string? FailureEmail { get; set; }
    public List<ForwardingTargetDTO> Targets { get; set; } = [];
}

/// <summary>Turns a forwarding master switch on or off; <see cref="Key"/> is one of the two switch keys in <see cref="ForwardingOptionKeys"/>.</summary>
public class ForwardingSwitchRequest {
    public string Key { get; set; } = "";
    public bool Enabled { get; set; }
}
