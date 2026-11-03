using System;
using System.Collections.Generic;

namespace Quartermaster.Api.Chapters;

public class ChapterUpdateRequest {
    public string Name { get; set; } = "";
    public string? ShortCode { get; set; }
    public string? KeycloakDepartment { get; set; }
    public string? ContactEmail { get; set; }
    public Guid? ParentChapterId { get; set; }

    /// <summary>Set by clients that send <see cref="ContactEmail"/>, <see cref="Level"/> and <see cref="TakeOverPeriodDays"/>.
    /// A request without it (a tab still running an older client) keeps the stored values.</summary>
    public bool IncludesContactAndTakeOver { get; set; }

    public ChapterLevel? Level { get; set; }
    public int? TakeOverPeriodDays { get; set; }
    public List<string> ExternalCodes { get; set; } = [];
    public List<Guid> AdministrativeDivisionIds { get; set; } = [];
}
