using System.Collections.Generic;

namespace Quartermaster.Api.Motions;

public class MotionListResponse : IPaginatedResponse<MotionDTO> {
    public List<MotionDTO> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
