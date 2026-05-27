using System;

namespace Quartermaster.Api.Chapters;

public class ChapterCreateRequest {
    public string Name { get; set; } = "";
    public string? ShortCode { get; set; }
    public string? ExternalCode { get; set; }
    public Guid? ParentChapterId { get; set; }
}
