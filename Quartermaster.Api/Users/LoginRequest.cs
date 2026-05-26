namespace Quartermaster.Api.Users;

public class LoginRequest {
    public string? Username { get; set; }
    public string? Email { get; set; }
    public required string Password { get; set; }
}
