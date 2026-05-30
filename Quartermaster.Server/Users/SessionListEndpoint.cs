using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Users;
using Quartermaster.Data.Tokens;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Users;

/// <summary>Active login tokens for the caller, with the request's own token flagged.</summary>
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
            IssuedAt = t.IssuedAt.ToDtoUtc(),
            ExpiresAt = t.Expires.ToDtoUtc(),
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
