using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Users;
using Quartermaster.Data.Tokens;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Users;

/// <summary>
/// Returns the calling user's active login tokens. The token that authenticated this
/// very request is marked <see cref="SessionDTO.IsCurrent"/> so the UI can label it.
/// Powers the "Meine Sitzungen" page.
/// </summary>
public class SessionListEndpoint : EndpointWithoutRequest<List<SessionDTO>> {
    private readonly TokenRepository _tokenRepo;
    private readonly PermissionContext _perms;

    public SessionListEndpoint(TokenRepository tokenRepo, PermissionContext perms) {
        _tokenRepo = tokenRepo;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/users/sessions");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        var userId = _perms.UserId;
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var currentTokenId = ResolveCurrentTokenId();
        var tokens = _tokenRepo.GetActiveLoginTokensForUser(userId.Value);
        var dtos = tokens.Select(t => new SessionDTO {
            TokenId = t.Id,
            IssuedAt = t.IssuedAt,
            ExpiresAt = t.Expires,
            IssuedIp = t.IssuedIp,
            IssuedUserAgent = t.IssuedUserAgent,
            IsCurrent = currentTokenId.HasValue && t.Id == currentTokenId.Value
        }).ToList();

        await SendAsync(dtos, cancellation: ct);
    }

    private Guid? ResolveCurrentTokenId() {
        var claim = User.FindFirst(AuthClaimTypes.TokenId);
        if (claim == null)
            return null;
        return Guid.TryParse(claim.Value, out var id) ? id : null;
    }
}
