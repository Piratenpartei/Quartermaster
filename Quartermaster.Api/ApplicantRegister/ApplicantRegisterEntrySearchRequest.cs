namespace Quartermaster.Api.ApplicantRegister;

public class ApplicantRegisterEntrySearchRequest : IPaginatedRequest {
    public string? Query { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
