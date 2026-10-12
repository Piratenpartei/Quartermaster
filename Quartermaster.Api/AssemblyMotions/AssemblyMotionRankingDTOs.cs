using System;
using System.Collections.Generic;

namespace Quartermaster.Api.AssemblyMotions;

public class AssemblyMotionRankingItemDTO {
    public int Rank { get; set; }
    public Guid Id { get; set; }
    public string DisplayNumber { get; set; } = "";
    public string Title { get; set; } = "";
    public Guid TypeId { get; set; }
    public string TypeCode { get; set; } = "";
    public string TypeName { get; set; } = "";
    public int TypeSortOrder { get; set; }
    public int UpvoteCount { get; set; }
    public AssemblyMotionStatus Status { get; set; }
}

public class AssemblyMotionRankingResponse {
    public List<AssemblyMotionRankingItemDTO> Items { get; set; } = [];
    public DateTimeOffset EndsAt { get; set; }
}
