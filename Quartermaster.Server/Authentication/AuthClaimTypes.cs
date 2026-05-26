namespace Quartermaster.Server.Authentication;

/// <summary>Custom claim names set by <see cref="TokenAuthenticationHandler"/>.</summary>
public static class AuthClaimTypes {
    /// <summary><see cref="System.Guid"/> of the <c>Tokens</c> row backing the request.</summary>
    public const string TokenId = "qm:token_id";
}
