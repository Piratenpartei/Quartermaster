using System.Collections.Generic;

namespace Quartermaster.Api.Motions;

public class MotionListResponse {
    public List<MotionDTO> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
