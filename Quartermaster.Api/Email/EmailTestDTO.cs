namespace Quartermaster.Api.Email;

public class EmailTestRequest {
    public string Recipient { get; set; } = "";
}

public class EmailTestResultDTO {
    public bool Success { get; set; }
    public string? Error { get; set; }
}
