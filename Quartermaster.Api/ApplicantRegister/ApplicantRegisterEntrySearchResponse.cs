using System.Collections.Generic;

namespace Quartermaster.Api.ApplicantRegister;

public class ApplicantRegisterEntrySearchResponse : IPaginatedResponse<ApplicantRegisterEntryDTO> {
    public List<ApplicantRegisterEntryDTO> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
