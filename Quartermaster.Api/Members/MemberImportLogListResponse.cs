using System.Collections.Generic;

namespace Quartermaster.Api.Members;

public class MemberImportLogListResponse : IPaginatedResponse<MemberImportLogDTO> {
    public List<MemberImportLogDTO> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
