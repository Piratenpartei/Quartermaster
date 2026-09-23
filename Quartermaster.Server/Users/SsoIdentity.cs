namespace Quartermaster.Server.Users;

public record SsoIdentity(string Email, int MemberNumber, string? FirstName, string? LastName);
