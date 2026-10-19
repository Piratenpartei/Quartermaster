using System;
using System.Collections.Generic;

namespace Quartermaster.Api.Chapters;

public class ChapterCreateRequest {
    public string Name { get; set; } = "";
    public string? ShortCode { get; set; }
    public string? KeycloakDepartment { get; set; }
    public Guid? ParentChapterId { get; set; }
    public List<string> ExternalCodes { get; set; } = [];
    public List<Guid> AdministrativeDivisionIds { get; set; } = [];
}
