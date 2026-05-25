using System.Collections.Generic;

namespace Quartermaster.Api.MembershipApplications;

public class MembershipApplicationListResponse : IPaginatedResponse<MembershipApplicationAdminDTO> {
    public List<MembershipApplicationAdminDTO> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
