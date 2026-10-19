namespace Quartermaster.Server.Users;

/// <summary>What a Keycloak login or account tells about a person; <see cref="Department"/> is the <c>ekklesia_department</c> attribute.</summary>
public record SsoIdentity(string Email, int MemberNumber, string? FirstName, string? LastName, string? Department = null);
