using System;

namespace Quartermaster.Api.ChapterAssociates;

public class ChapterOfficerSearchRequest : IPaginatedRequest {
    public string? Query { get; set; }
    public Guid? ChapterId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
