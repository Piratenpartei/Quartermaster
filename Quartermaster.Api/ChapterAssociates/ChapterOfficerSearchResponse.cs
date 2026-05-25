using System.Collections.Generic;

namespace Quartermaster.Api.ChapterAssociates;

public class ChapterOfficerSearchResponse : IPaginatedResponse<ChapterOfficerDTO> {
    public List<ChapterOfficerDTO> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
