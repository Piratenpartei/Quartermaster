using System;
using System.Collections.Generic;

namespace Quartermaster.Api.ChapterMails;

public class ChapterMailDTO {
    public Guid Id { get; set; }
    public Guid ChapterId { get; set; }
    public string ChapterName { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Subject { get; set; }
    public ChapterMailTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public string TargetName { get; set; } = "";
    public bool IncludeDescendants { get; set; } = true;
    public Guid? TemplateId { get; set; }
    public string? TemplateDisplayName { get; set; }
    public string? Content { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string? CreatedByDisplayName { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public Guid? SentByUserId { get; set; }
    public string? SentByDisplayName { get; set; }
    public int? RecipientCount { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public bool IsSent => SentAt.HasValue;
    public bool IsArchived => DeletedAt.HasValue;
    public bool IsDraft => !IsSent && !IsArchived;
}

public class ChapterMailListItemDTO {
    public Guid Id { get; set; }
    public Guid ChapterId { get; set; }
    public string ChapterName { get; set; } = "";
    public string Title { get; set; } = "";
    public ChapterMailTargetType TargetType { get; set; }
    public string TargetName { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public int? RecipientCount { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public bool IsSent => SentAt.HasValue;
    public bool IsArchived => DeletedAt.HasValue;
}

public class ChapterMailListResponse {
    public List<ChapterMailListItemDTO> Items { get; set; } = new();
}

public class ChapterMailCreateRequest {
    public Guid ChapterId { get; set; }
    public string Title { get; set; } = "";
    public string? Subject { get; set; }
    public ChapterMailTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public bool IncludeDescendants { get; set; } = true;
    public Guid? TemplateId { get; set; }
    public string? Content { get; set; }
}

public class ChapterMailUpdateRequest {
    public string Title { get; set; } = "";
    public string? Subject { get; set; }
    public ChapterMailTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public bool IncludeDescendants { get; set; } = true;
    public Guid? TemplateId { get; set; }
    public string? Content { get; set; }
}

public enum ChapterMailTargetType {
    Chapter = 0,
    AdministrativeDivision = 1
}
