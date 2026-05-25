using System.Collections.Generic;

namespace Quartermaster.Api.AdministrativeDivisions;

public class AdministrativeDivisionSearchResponse : IPaginatedResponse<AdministrativeDivisionDTO> {
    public List<AdministrativeDivisionDTO> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
