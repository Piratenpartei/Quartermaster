using System.Collections.Generic;

namespace Quartermaster.Api.Members;

public class MemberSearchResponse {
    public List<MemberDTO> Items { get; set; } = new();
    public int TotalCount { get; set; }
}
