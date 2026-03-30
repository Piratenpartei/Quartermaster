using System;

namespace Quartermaster.Api.Members;

public class MemberSearchRequest {
    public string? Query { get; set; }
    public Guid? ChapterId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
