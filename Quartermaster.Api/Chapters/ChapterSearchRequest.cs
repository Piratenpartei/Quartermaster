namespace Quartermaster.Api.Chapters;

public class ChapterSearchRequest : IPaginatedRequest {
    public string? Query { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
