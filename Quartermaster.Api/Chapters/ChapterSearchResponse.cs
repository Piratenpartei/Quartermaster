using System.Collections.Generic;

namespace Quartermaster.Api.Chapters;

public class ChapterSearchResponse : IPaginatedResponse<ChapterDTO> {
    public List<ChapterDTO> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
