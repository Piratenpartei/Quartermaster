using System.Collections.Generic;

namespace Quartermaster.Api.Chapters;

public class ChapterSearchResponse {
    public List<ChapterDTO> Items { get; set; } = new();
    public int TotalCount { get; set; }
}
