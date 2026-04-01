namespace Quartermaster.Api.Users;

public class LoginRequest {
    public string? Username { get; set; }
    public string? EMail { get; set; }
    public required string Password { get; set; }
}
