using System.Collections.Generic;

namespace Quartermaster.Api.DueSelector;

public class DueSelectionListResponse : IPaginatedResponse<DueSelectionAdminDTO> {
    public List<DueSelectionAdminDTO> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
