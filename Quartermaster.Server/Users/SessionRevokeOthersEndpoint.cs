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

/// <summary>Revokes every token for the caller except the current one. Returns the revoked count.</summary>
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
            // Refuse rather than risk revoking the caller's own token along with the others.
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
