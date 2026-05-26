namespace Quartermaster.Server.Authentication;

/// <summary>
/// Custom claim type names attached to the ticket by <see cref="TokenAuthenticationHandler"/>
/// alongside the standard <c>ClaimTypes.*</c> claims. Keeping them in one place so consumers
/// don't sprinkle magic strings.
/// </summary>
public static class AuthClaimTypes {
    /// <summary>The <see cref="System.Guid"/> of the <c>Tokens</c> row that authenticated this request.</summary>
    public const string TokenId = "qm:token_id";
}
