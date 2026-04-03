using System;

namespace Quartermaster.Api.MembershipApplications;

public class MembershipApplicationListRequest : IPaginatedRequest {
    public Guid? ChapterId { get; set; }
    public int? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
