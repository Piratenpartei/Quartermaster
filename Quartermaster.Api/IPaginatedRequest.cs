namespace Quartermaster.Api;

public interface IPaginatedRequest {
    int Page { get; set; }
    int PageSize { get; set; }
}
