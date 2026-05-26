using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Data.Tokens;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Users;

public class SessionRevokeRequest {
    public Guid Id { get; set; }
}

/// <summary>Revokes one of the caller's tokens. Idempotent 204 — unowned/missing both 204 to avoid leaking ownership.</summary>
public class SessionRevokeEndpoint : Endpoint<SessionRevokeRequest> {
    private readonly TokenRepository _tokenRepo;
    private readonly PermissionContext _perms;

    public SessionRevokeEndpoint(TokenRepository tokenRepo, PermissionContext perms) {
        _tokenRepo = tokenRepo;
        _perms = perms;
    }

    public override void Configure() {
        Delete("/api/users/sessions/{Id}");
    }

    public override async Task HandleAsync(SessionRevokeRequest req, CancellationToken ct) {
        var userId = _perms.UserId;
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        _tokenRepo.DeleteOwnedByUser(req.Id, userId.Value);
        await SendNoContentAsync(ct);
    }
}
