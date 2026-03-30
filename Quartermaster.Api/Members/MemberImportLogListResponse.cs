using System.Collections.Generic;

namespace Quartermaster.Api.Members;

public class MemberImportLogListResponse {
    public List<MemberImportLogDTO> Items { get; set; } = new();
    public int TotalCount { get; set; }
}
