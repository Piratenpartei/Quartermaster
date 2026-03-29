namespace Quartermaster.Api.DueSelector;

public class DueSelectionListRequest {
    public int? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
