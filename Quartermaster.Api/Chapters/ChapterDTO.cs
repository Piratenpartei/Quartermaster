using System;
using System.Collections.Generic;

namespace Quartermaster.Api.Chapters;

public class ChapterDTO {
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? ShortCode { get; set; }
    public string? KeycloakDepartment { get; set; }
    public Guid? ParentChapterId { get; set; }
    public List<string> ExternalCodes { get; set; } = [];
    public List<Guid> AdministrativeDivisionIds { get; set; } = [];
    public DateTimeOffset? ArchivedAt { get; set; }

    /// <summary>Only filled for signed-in callers. In templates it falls back to the federal contact address.</summary>
    public string? ContactEmail { get; set; }

    public bool IsArchived => ArchivedAt.HasValue;
}
