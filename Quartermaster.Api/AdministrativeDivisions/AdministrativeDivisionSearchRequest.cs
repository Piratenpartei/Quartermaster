namespace Quartermaster.Api.AdministrativeDivisions;

public class AdministrativeDivisionSearchRequest : IPaginatedRequest {
    public string? Query { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
