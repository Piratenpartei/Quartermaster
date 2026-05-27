using System;

namespace Quartermaster.Api.Chapters;

public class ChapterUpdateRequest {
    public string Name { get; set; } = "";
    public string? ShortCode { get; set; }
    public string? ExternalCode { get; set; }
    public Guid? ParentChapterId { get; set; }
    public Guid? AdministrativeDivisionId { get; set; }
}
