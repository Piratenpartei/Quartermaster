using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Data.Tokens;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Users;

public class SessionRevokeOthersResponse {
    public int Revoked { get; set; }
}

/// <summary>
/// Logs the calling user out of every device EXCEPT the one making this request. Useful
/// after a "did I leave my laptop logged in somewhere?" moment. Returns the number of
/// rows deleted (purely informational; the UI can decide whether to surface it).
/// </summary>
public class SessionRevokeOthersEndpoint : EndpointWithoutRequest<SessionRevokeOthersResponse> {
    private readonly TokenRepository _tokenRepo;
    private readonly PermissionContext _perms;

    public SessionRevokeOthersEndpoint(TokenRepository tokenRepo, PermissionContext perms) {
        _tokenRepo = tokenRepo;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/users/sessions/revoke-others");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        var userId = _perms.UserId;
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var currentTokenId = ResolveCurrentTokenId();
        if (currentTokenId == null) {
            // We can't tell which token is the caller's, so refuse rather than risk
            // logging the caller out alongside the rest.
            await SendUnauthorizedAsync(ct);
            return;
        }

        var revoked = _tokenRepo.DeleteOtherLoginTokensForUser(userId.Value, currentTokenId.Value);
        await SendAsync(new SessionRevokeOthersResponse { Revoked = revoked }, cancellation: ct);
    }

    private Guid? ResolveCurrentTokenId() {
        var claim = User.FindFirst(AuthClaimTypes.TokenId);
        if (claim == null)
            return null;
        return Guid.TryParse(claim.Value, out var id) ? id : null;
    }
}
