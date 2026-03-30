using System;

namespace Quartermaster.Api.Chapters;

public class ChapterDTO {
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? ShortCode { get; set; }
    public Guid? AdministrativeDivisionId { get; set; }
    public string? ExternalCode { get; set; }
    public Guid? ParentChapterId { get; set; }
}
