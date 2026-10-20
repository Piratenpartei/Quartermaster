namespace Quartermaster.Server.Users;

/// <summary>What a Keycloak login or account tells about a person; <see cref="Department"/> is the <c>ekklesia_department</c> attribute,
/// <see cref="Eligible"/> the <c>ekklesia_eligible</c> attribute (null when the login does not carry it).</summary>
public record SsoIdentity(string Email, int MemberNumber, string? FirstName, string? LastName, string? Department = null, bool? Eligible = null);
