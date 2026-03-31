using System.Collections.Generic;

namespace Quartermaster.Api.ChapterAssociates;

public class ChapterOfficerSearchResponse {
    public List<ChapterOfficerDTO> Items { get; set; } = new();
    public int TotalCount { get; set; }
}
