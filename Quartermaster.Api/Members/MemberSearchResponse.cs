using System.Collections.Generic;

namespace Quartermaster.Api.Members;

public class MemberSearchResponse : IPaginatedResponse<MemberDTO> {
    public List<MemberDTO> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
