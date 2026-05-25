using System.Collections.Generic;

namespace Quartermaster.Api;

public interface IPaginatedResponse<T> {
    List<T> Items { get; set; }
    int TotalCount { get; set; }
    int Page { get; set; }
    int PageSize { get; set; }
}
